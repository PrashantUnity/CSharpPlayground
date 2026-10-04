// Modern Dart 3 Showcase Script
// Demonstrating records, pattern matching, sealed classes, and async execution.

(int, int) calculateMinMax(List<int> numbers) {
  var min = numbers.reduce((a, b) => a < b ? a : b);
  var max = numbers.reduce((a, b) => a > b ? a : b);
  return (min, max);
}

sealed class Shape {}
class Circle extends Shape {
  final double radius;
  Circle(this.radius);
}
class Rectangle extends Shape {
  final double width;
  final double height;
  Rectangle(this.width, this.height);
}

double calculateArea(Shape shape) => switch (shape) {
  Circle(radius: var r) => 3.141592653589793 * r * r,
  Rectangle(width: var w, height: var h) => w * h,
};

Future<void> main() async {
  print('==============================================');
  print('🚀 Welcome to Dart in FrySharp!');
  print('==============================================');

  var scores = [42, 88, 15, 99, 73, 105];
  var (minVal, maxVal) = calculateMinMax(scores);
  print('Scores: $scores');
  print('Computed Range => Min: $minVal, Max: $maxVal');

  Shape shape = Circle(5.0);
  print('Area of Circle(5.0): ${calculateArea(shape)}');

  print('\nDart 3 script executed successfully.');
}
