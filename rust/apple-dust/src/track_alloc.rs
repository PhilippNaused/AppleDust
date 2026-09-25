use std::{
    alloc::GlobalAlloc,
    sync::atomic::{AtomicU64, Ordering},
};

const ORDERING: Ordering = Ordering::Relaxed;

/// A global alloc wrapper that let's apple-dust track memory allocations of your benchmark.
///
/// Requires the `track_alloc` feature.
///
/// # Example
/// ```
/// use apple_dust::*;
///
/// #[global_allocator]
/// static ALLOCATOR: AllocTracker<std::alloc::System> = AllocTracker::system();
/// ```
pub struct AllocTracker<A: GlobalAlloc> {
    inner: A,
}

impl AllocTracker<std::alloc::System> {
    /// Wraps the system allocator.
    pub const fn system() -> Self {
        Self::new(std::alloc::System)
    }
}

impl<A: GlobalAlloc> AllocTracker<A> {
    /// Wraps a custom allocator.
    pub const fn new(inner: A) -> Self {
        Self { inner }
    }
}

pub fn get_total_bytes() -> u64 {
    GLOBAL_ALLOC_COUNTER.load(ORDERING)
}

pub fn reset() {
    GLOBAL_ALLOC_COUNTER.store(0, ORDERING);
}

static GLOBAL_ALLOC_COUNTER: AtomicU64 = AtomicU64::new(0);

unsafe impl<A: GlobalAlloc> GlobalAlloc for AllocTracker<A> {
    #[inline]
    unsafe fn alloc(&self, layout: std::alloc::Layout) -> *mut u8 {
        let ptr = unsafe { self.inner.alloc(layout) };
        if !ptr.is_null() {
            GLOBAL_ALLOC_COUNTER.fetch_add(layout.size() as u64, ORDERING);
        }
        ptr
    }

    #[inline]
    unsafe fn dealloc(&self, ptr: *mut u8, layout: std::alloc::Layout) {
        unsafe { self.inner.dealloc(ptr, layout) };
    }
}
