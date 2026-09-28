"""
Folding demo for Python — every block kind the studio can collapse.
"""
import sys
import math


class Shape:
    """Base class for geometric shapes."""

    def __init__(self, color: str = "red"):
        self.color = color

    def area(self) -> float:
        raise NotImplementedError

    def describe(self) -> str:
        return f"{self.__class__.__name__}(color={self.color}, area={self.area():.2f})"


class Circle(Shape):
    """A circle with a given radius."""

    def __init__(self, radius: float, color: str = "blue"):
        super().__init__(color)
        self.radius = radius

    def area(self) -> float:
        return math.pi * self.radius ** 2

    def circumference(self) -> float:
        return 2 * math.pi * self.radius


class Rectangle(Shape):
    def __init__(self, width: float, height: float):
        super().__init__("green")
        self.width = width
        self.height = height

    def area(self) -> float:
        return self.width * self.height


def classify(shapes: list) -> dict:
    """Classify shapes by type, returning a dict of lists."""
    result = {}
    for shape in shapes:
        key = type(shape).__name__
        if key not in result:
            result[key] = []
        result[key].append(shape)
    return result


def print_summary(shapes: list) -> None:
    for shape in shapes:
        if shape.area() > 100:
            print(f"  Large: {shape.describe()}")
        elif shape.area() > 10:
            print(f"  Medium: {shape.describe()}")
        else:
            print(f"  Small: {shape.describe()}")


def main():
    shapes = [
        Circle(5),
        Circle(1),
        Rectangle(3, 4),
        Rectangle(20, 10),
    ]

    print("All shapes:")
    print_summary(shapes)

    try:
        groups = classify(shapes)
        for name, group in groups.items():
            print(f"\n{name}s ({len(group)}):")
            for s in group:
                print(f"  {s.describe()}")
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
    finally:
        print("\nDone.")


if __name__ == "__main__":
    main()
