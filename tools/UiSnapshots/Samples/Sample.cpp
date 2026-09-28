// #vcpkg: fmt
#include <iostream>
#include <vector>
#include <string>

int main() {
    std::vector<std::string> greetings = { "Hello", "C++", "Studio" };
    for (const auto& word : greetings) {
        std::cout << word << " ";
    }
    std::cout << std::endl;
    return 0;
}
