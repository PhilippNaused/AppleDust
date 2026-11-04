use std::{
    alloc::{GlobalAlloc, Layout},
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

#[inline(always)]
fn inc_global_counter(size: usize) {
    GLOBAL_ALLOC_COUNTER.fetch_add(size as u64, ORDERING);
}

unsafe impl<A: GlobalAlloc> GlobalAlloc for AllocTracker<A> {
    #[inline(always)]
    unsafe fn alloc(&self, layout: Layout) -> *mut u8 {
        let ptr = unsafe { self.inner.alloc(layout) };
        if !ptr.is_null() {
            inc_global_counter(layout.size());
        }
        ptr
    }

    #[inline(always)]
    unsafe fn dealloc(&self, ptr: *mut u8, layout: Layout) {
        unsafe { self.inner.dealloc(ptr, layout) };
    }

    #[inline(always)]
    unsafe fn alloc_zeroed(&self, layout: Layout) -> *mut u8 {
        let ptr = unsafe { self.inner.alloc_zeroed(layout) };
        if !ptr.is_null() {
            inc_global_counter(layout.size());
        }
        ptr
    }

    #[inline(always)]
    unsafe fn realloc(&self, ptr: *mut u8, layout: Layout, new_size: usize) -> *mut u8 {
        let new_ptr = unsafe { self.inner.realloc(ptr, layout, new_size) };
        if !new_ptr.is_null() {
            if ptr != new_ptr {
                inc_global_counter(new_size); // complete new allocation
            } else if new_size > layout.size() {
                inc_global_counter(new_size - layout.size()); // grow
            } else {
                // shrink
            }
        }
        new_ptr
    }
}
