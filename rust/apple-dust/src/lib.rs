use std::cmp::max;
use std::env;
use std::fmt::Debug;
use std::time::{Duration, Instant};

#[cfg(feature = "track_alloc")]
mod track_alloc;
mod worker;

#[cfg(feature = "track_alloc")]
pub use track_alloc::AllocTracker;
pub use core::hint::black_box;

#[derive(Default)]
pub struct BenchmarkBuilder<'a> {
    benchmarks: Vec<Box<dyn Benchmark + 'a>>,
}

const UNROLL_FACTOR: u64 = 4;
const _A: () = assert!(UNROLL_FACTOR > 0, "The unroll factor cannot be 0");
const SAMPLE_OVERHEAD: &str = "Overhead";

impl<'a> BenchmarkBuilder<'a> {
    fn add_benchmark<B: Benchmark + 'a>(&mut self, b: B) {
        self.benchmarks.push(Box::new(b));
    }

    pub fn add_function<O, F>(mut self, func: F, name: &'static str) -> Self
    where
        F: FnMut() -> O + 'a,
        O: 'a,
    {
        self.add_benchmark(BenchmarkImpl::new(func, name));
        self
    }

    pub fn add_overhead<O, F>(self, func: F) -> Self
    where
        F: FnMut() -> O + 'a,
        O: 'a,
    {
        self.add_function(func, SAMPLE_OVERHEAD)
    }

    fn build(mut self) -> worker::AppleWorker<'a> {
        if self.benchmarks.is_empty() {
            panic!("No benchmarks added");
        }
        if self.benchmarks.iter().all(|b| b.name() != SAMPLE_OVERHEAD) {
            self = self.add_overhead(|| 0); // ~100 ps
        }
        worker::AppleWorker::new(self.benchmarks)
    }

    pub fn test(self) -> ! {
        self.build().test()
    }

    pub fn run(self) -> std::io::Result<()> {
        let worker = self.build();
        if env::args().any(|a| a == "test") {
            worker.test();
        }
        worker.work()
    }
}

#[derive(Clone, Copy)]
struct Sample {
    iters: u64,
    nanos: u64,
    bytes: u64,
}

impl Sample {
    pub fn get_ns_per_iter(&self) -> f64 {
        (self.nanos as f64) / (self.iters as f64)
    }
    pub fn get_bytes_per_iter(&self) -> f64 {
        (self.bytes as f64) / (self.iters as f64)
    }
}

impl Debug for Sample {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Sample")
            .field("iters", &self.iters)
            .field("nanos", &self.nanos)
            .field("time", &as_time(self.get_ns_per_iter()))
            .finish()
    }
}

trait Benchmark {
    fn iters(&self) -> u64;
    fn name(&self) -> &'static str;
    fn sample(&mut self, iters: u64) -> Sample;
    fn pilot(&mut self, target: Duration);
}

#[derive(Debug)]
struct BenchmarkImpl<T, F>
where
    F: FnMut() -> T,
{
    func: F,
    _name: &'static str,
    iters: u64,
}

impl<T, F> BenchmarkImpl<T, F>
where
    F: FnMut() -> T,
{
    pub const fn new(func: F, _name: &'static str) -> Self {
        Self {
            func,
            _name,
            iters: UNROLL_FACTOR,
        }
    }

    #[inline(never)]
    fn sample_inner(&mut self, iters: u64) -> u64 {
        let iters = iters / UNROLL_FACTOR;
        let time_start = Instant::now();
        for _ in 0..iters {
            for _ in 0..UNROLL_FACTOR {
                _ = black_box((self.func)());
            }
        }
        time_start.elapsed().as_nanos() as u64
    }
}

impl<T, F> Benchmark for BenchmarkImpl<T, F>
where
    F: FnMut() -> T,
{
    #[inline]
    fn iters(&self) -> u64 {
        self.iters
    }

    #[inline]
    fn name(&self) -> &'static str {
        self._name
    }

    #[inline(never)]
    fn sample(&mut self, iters: u64) -> Sample {
        #[cfg(feature = "track_alloc")]
        {
            track_alloc::reset();
            let nanos = self.sample_inner(iters);
            let bytes = track_alloc::get_total_bytes();
            Sample {
                iters,
                nanos,
                bytes,
            }
        }
        #[cfg(not(feature = "track_alloc"))]
        {
            let nanos = self.sample_inner(iters);
            Sample {
                iters,
                nanos,
                bytes: 0,
            }
        }
    }

    fn pilot(&mut self, target: Duration) {
        let target = target.as_nanos() as u64;
        let mut time;
        loop {
            time = self.sample(self.iters).nanos;
            if time > (target as f64 * 1.1) as u64 {
                break;
            }
            self.iters *= 2;
        }
        let ratio: f64 = (target as f64) / (time as f64);
        self.iters = ((self.iters as f64) * ratio) as u64;
        self.iters = max(self.iters, UNROLL_FACTOR)
    }
}

fn as_time(ns: f64) -> String {
    match ns {
        ns if ns < 1.0 => format!("{} ps", short(ns * 1e3)),
        ns if ns < 1.0e3 => format!("{} ns", short(ns)),
        ns if ns < 1.0e6 => format!("{} µs", short(ns / 1e3)),
        ns if ns < 1.0e9 => format!("{} ms", short(ns / 1e6)),
        _ => format!("{} s ", short(ns / 1e9)),
    }
}

fn short(n: f64) -> String {
    match n {
        n if n < 100.0 => format!("{:>7.3}", n),
        n if n < 1000.0 => format!("{:<7.3}", n),
        _ => format!("{:<7.2}", n),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn format_short() {
        const TEST_VALUES: &[(f64, &str)] = &[
            (1000.0, "1000.00"),
            (100.00, "100.000"),
            (10.000, " 10.000"),
            (1.0000, "  1.000"),
            (0.1000, "  0.100"),
            (0.0100, "  0.010"),
            (0.0010, "  0.001"),
        ];

        for &(ns, expected) in TEST_VALUES {
            assert_eq!(short(ns), expected);
        }
    }

    #[test]
    fn format_time() {
        const TEST_VALUES: &[(f64, &str)] = &[
            (100_000_000_000.0, "100.000 s "),
            (1_000_000_000.0, "  1.000 s "),
            (1_000_000.0, "  1.000 ms"),
            (1_000.0, "  1.000 µs"),
            (100.0, "100.000 ns"),
            (10.0, " 10.000 ns"),
            (1.0, "  1.000 ns"),
            (0.1, "100.000 ps"),
            (0.01, " 10.000 ps"),
            (0.001, "  1.000 ps"),
            (0.000001, "  0.001 ps"),
        ];

        for &(ns, expected) in TEST_VALUES {
            assert_eq!(as_time(ns), expected);
        }
    }
}
