#pragma once
#include <cstddef>
// Single-threaded allocation accounting used only by protected resource tests.
std::ptrdiff_t live_allocations();
