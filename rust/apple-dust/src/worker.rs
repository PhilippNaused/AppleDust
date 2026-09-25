use std::io::Write;
use std::time::Duration;

use crate::SAMPLE_OVERHEAD;
use crate::as_time;

use super::Benchmark;
use super::Sample;

pub struct StdRpc {
    read: std::io::Stdin,
    write: std::io::Stdout,
}

impl StdRpc {
    pub fn new() -> Self {
        Self {
            read: std::io::stdin(),
            write: std::io::stdout(),
        }
    }
}

pub struct AppleWorker<'a> {
    rpc: StdRpc,
    benchmarks: Vec<Box<dyn Benchmark + 'a>>,
}

impl<'a> AppleWorker<'a> {
    pub fn new(benchmarks: Vec<Box<dyn Benchmark + 'a>>) -> Self {
        Self {
            rpc: StdRpc::new(),
            benchmarks,
        }
    }

    fn find_benchmark(&mut self, name: &str) -> &mut Box<dyn Benchmark + 'a> {
        self.benchmarks
            .iter_mut()
            .find(|b| b.name() == name)
            .unwrap_or_else(|| panic!("Benchmark not found: {}", name))
    }

    fn warmup(&mut self, name: &str, target_ms: u64) -> u64 {
        let dur = std::time::Duration::from_millis(target_ms);
        let b = self.find_benchmark(name);
        b.pilot(dur);
        b.iters()
    }

    fn get_sample(&mut self, name: &str, iters: u64) -> Sample {
        self.find_benchmark(name).sample(iters)
    }

    fn mk_error(msg: &str) -> std::io::Error {
        std::io::Error::new(std::io::ErrorKind::InvalidInput, msg)
    }

    pub fn test(mut self) -> ! {
        println!("testing...");
        let mut overhead = 0.0;
        for b in &mut self.benchmarks {
            println!("warming up: {}", b.as_ref().name());
            b.as_mut().pilot(Duration::from_millis(500));
        }
        loop {
            for b in &mut self.benchmarks {
                let b = b.as_mut();
                let sample = b.sample(b.iters());
                let mut ns = sample.get_ns_per_iter();
                if b.name() == SAMPLE_OVERHEAD {
                    overhead = ns;
                } else {
                    ns -= overhead;
                }
                if cfg!(feature = "track_alloc") {
                    let bpi = sample.get_bytes_per_iter();
                    println!("{:>20}: {} {:>8.0} B", b.name(), as_time(ns), bpi);
                } else {
                    println!("{:>20}: {}", b.name(), as_time(ns));
                }
            }
        }
    }

    pub fn work(mut self) -> std::io::Result<()> {
        loop {
            let mut line = String::new();
            self.rpc.read.read_line(&mut line)?;
            let command = parse_command(&line).map_err(Self::mk_error)?;
            match command {
                Command::WarmUp { name, target_ms } => {
                    let iters = self.warmup(name, target_ms);
                    writeln!(self.rpc.write, "{}", iters)?;
                }
                Command::GetSample { name, iters } => {
                    let sample = self.get_sample(name, iters);
                    if cfg!(feature = "track_alloc") {
                        writeln!(self.rpc.write, "{},{}", sample.nanos, sample.bytes)?;
                    } else {
                        writeln!(self.rpc.write, "{},-1", sample.nanos)?;
                    }
                }
                Command::GetNames => {
                    let names = self.get_names();
                    writeln!(self.rpc.write, "{}", names.join(","))?;
                }
                Command::Exit => {
                    return Ok(());
                }
            }
        }
    }

    fn get_names(&self) -> Vec<&str> {
        self.benchmarks.iter().map(|b| b.name()).collect()
    }
}

enum Command<'a> {
    WarmUp { name: &'a str, target_ms: u64 },
    GetSample { name: &'a str, iters: u64 },
    GetNames,
    Exit,
}

fn parse_command<'a>(line: &'a str) -> Result<Command<'a>, &'static str> {
    let parts: Vec<&str> = line.trim().split('|').collect();
    let command = *parts.first().ok_or("Missing command")?;
    let args = &parts[1..];
    match command {
        "WarmUp" => {
            let name = args.first().ok_or("Missing benchmark name")?;
            let target_ms = args
                .get(1)
                .ok_or("Missing target_ms")?
                .parse::<u64>()
                .map_err(|_| "Invalid target_ms")?;
            Ok(Command::WarmUp { name, target_ms })
        }
        "GetSample" => {
            let name = args.first().ok_or("Missing benchmark name")?;
            let iters = args
                .get(1)
                .ok_or("Missing iters")?
                .parse::<u64>()
                .map_err(|_| "Invalid iters")?;
            Ok(Command::GetSample { name, iters })
        }
        "GetNames" => Ok(Command::GetNames),
        "Exit" => Ok(Command::Exit),
        _ => Err("Unknown command"),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_command_warmup() {
        let line = "WarmUp|benchmark1|1000";
        let command = parse_command(line).unwrap();
        match command {
            Command::WarmUp { name, target_ms } => {
                assert_eq!(name, "benchmark1");
                assert_eq!(target_ms, 1000);
            }
            _ => panic!("Expected WarmUp command"),
        }
    }

    #[test]
    fn parse_command_get_sample() {
        let line = "GetSample|benchmark1|10";
        let command = parse_command(line).unwrap();
        match command {
            Command::GetSample { name, iters } => {
                assert_eq!(name, "benchmark1");
                assert_eq!(iters, 10);
            }
            _ => panic!("Expected GetSample command"),
        }
    }

    #[test]
    fn parse_command_get_names() {
        let line = "GetNames";
        let command = parse_command(line).unwrap();
        match command {
            Command::GetNames => {}
            _ => panic!("Expected GetNames command"),
        }
    }
}
