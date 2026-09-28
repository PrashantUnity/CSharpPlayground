/**
 * Folding demo for JavaScript — brace blocks, comments, arrow functions.
 */

class Shape {
    constructor(color = "red") {
        this.color = color;
    }

    area() {
        throw new Error("Not implemented");
    }

    describe() {
        return `${this.constructor.name}(color=${this.color}, area=${this.area().toFixed(2)})`;
    }
}

class Circle extends Shape {
    constructor(radius, color = "blue") {
        super(color);
        this.radius = radius;
    }

    area() {
        return Math.PI * this.radius ** 2;
    }

    circumference() {
        return 2 * Math.PI * this.radius;
    }
}

class Rectangle extends Shape {
    constructor(width, height) {
        super("green");
        this.width = width;
        this.height = height;
    }

    area() {
        return this.width * this.height;
    }
}

function classify(shapes) {
    return shapes.reduce((acc, shape) => {
        const key = shape.constructor.name;
        if (!acc[key]) {
            acc[key] = [];
        }
        acc[key].push(shape);
        return acc;
    }, {});
}

function printSummary(shapes) {
    for (const shape of shapes) {
        if (shape.area() > 100) {
            console.log(`  Large: ${shape.describe()}`);
        } else if (shape.area() > 10) {
            console.log(`  Medium: ${shape.describe()}`);
        } else {
            console.log(`  Small: ${shape.describe()}`);
        }
    }
}

const shapes = [
    new Circle(5),
    new Circle(1),
    new Rectangle(3, 4),
    new Rectangle(20, 10),
];

console.log("All shapes:");
printSummary(shapes);

const groups = classify(shapes);
for (const [name, group] of Object.entries(groups)) {
    console.log(`\n${name}s (${group.length}):`);
    group.forEach(s => console.log(`  ${s.describe()}`));
}
