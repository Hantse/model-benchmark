#include "support.hpp"
#include <iostream>
#include <map>

int main(int argc, char** argv) {
    std::map<std::string, std::function<void()>> cases;
    for (const auto& group : {admission_cases(), lease_cases(), recovery_cases()})
        for (const auto& [name, function] : group)
            if (!cases.emplace(name, function).second) return 2;
    if (cases.size() != 108) return 2;
    if (argc == 2 && std::string(argv[1]) == "--list") {
        for (const auto& [name, function] : cases) { (void)function; std::cout << name << '\n'; }
        return 0;
    }
    if (argc != 2 || !cases.contains(argv[1])) {
        std::cerr << "Expected one registered acceptance case name\n";
        return 2;
    }
    try {
        cases.at(argv[1])();
        std::cout << "PASS " << argv[1] << '\n';
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << argv[1] << ": " << error.what() << '\n';
        return 1;
    } catch (...) {
        std::cerr << "FAIL " << argv[1] << ": unexpected exception\n";
        return 1;
    }
}
