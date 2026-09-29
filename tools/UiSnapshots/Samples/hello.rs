// A Rust file for the Code Studio: builds with Cargo and runs on the toolchain installed on this computer.
// Add a crate with a comment such as:  // #crate: rand = "0.8"
use fry::prelude::*;

#[derive(Debug, Clone)]
struct Planet {
    name: &'static str,
    moons: u32,
    gravity: f64,
}

fn main() {
    println!("Hello from Rust!");

    let planets = vec![
        Planet { name: "Earth", moons: 1, gravity: 9.81 },
        Planet { name: "Mars", moons: 2, gravity: 3.71 },
        Planet { name: "Jupiter", moons: 95, gravity: 24.79 },
    ];

    let total: u32 = planets.iter().map(|p| p.moons).sum();
    println!("{} moons in total", total);

    // A list of structs becomes a table with a column per field in the Results deck.
    table(&planets, "Planets");
}
