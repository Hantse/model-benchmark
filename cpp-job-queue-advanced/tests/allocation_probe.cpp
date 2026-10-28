#include "allocation_probe.hpp"
#include <cstdlib>
#include <new>

namespace { std::ptrdiff_t live = 0; }
std::ptrdiff_t live_allocations() { return live; }
void* operator new(std::size_t size) {
    if (void* pointer = std::malloc(size == 0 ? 1 : size)) { ++live; return pointer; }
    throw std::bad_alloc();
}
void operator delete(void* pointer) noexcept {
    if (pointer) { --live; std::free(pointer); }
}
void* operator new[](std::size_t size) { return ::operator new(size); }
void operator delete[](void* pointer) noexcept { ::operator delete(pointer); }
void operator delete(void* pointer, std::size_t) noexcept { ::operator delete(pointer); }
void operator delete[](void* pointer, std::size_t) noexcept { ::operator delete(pointer); }
