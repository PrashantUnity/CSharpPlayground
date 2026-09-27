import java.util.Scanner;

public class InteractiveGreeter {
    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);
        System.out.println("=== Java Interactive Console ===");
        System.out.print("Please enter your name: ");
        String name = scanner.nextLine();

        System.out.println("Welcome to C# Code Studio, " + name + "!");
        System.out.println("Java JDK " + System.getProperty("java.version") + " is running your program.");
    }
}
