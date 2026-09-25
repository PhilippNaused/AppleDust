use std::{thread, time::Duration};

use apple_dust::*;

#[cfg(feature = "track_alloc")]
#[global_allocator]
static ALLOCATOR: AllocTracker<std::alloc::System> = AllocTracker::system();

// each call to black_box adds ~90-110 ps overhead

#[expect(clippy::redundant_closure)]
fn main() -> Result<(), std::io::Error> {
    BenchmarkBuilder::default()
        .add_function(
            || thread::sleep(Duration::ZERO),
            "sleep0",
        )
        .add_function(
            || thread::sleep(Duration::from_nanos(1)),
            "sleep1ns",
        )
        .add_function(
            || thread::yield_now(),
            "yield_now",
        )
        .add_function(|| vec![0u8; 1234], "test_alloc")
        .run()
}
