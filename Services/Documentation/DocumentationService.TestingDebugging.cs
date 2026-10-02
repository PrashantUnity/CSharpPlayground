using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildTestingDebuggingCategory()
    {
        return new DocCategory
        {
            Id = "testing_debugging",
            Title = "Testing & Debugging",
            IconKind = MaterialIconKind.TestTube,
            AccentColor = "#FB7185",
            Badge = "Quality",
            Description = "Unit testing, mocking, integration testing, and debugging techniques in .NET.",
            Articles = new List<DocArticle>
            {
                CreateTestingDebuggingArticle()
            }
        };
    }

    private DocArticle CreateTestingDebuggingArticle()
    {
        return new DocArticle
        {
            Id = "learn_testing_debugging",
            Title = "Testing & Debugging: xUnit, Moq & the Debugger",
            Subtitle = "Write trustworthy unit and integration tests, isolate dependencies with mocks, and debug efficiently when something still goes wrong.",
            ReadingTime = "7 min read",
            Summary = "Automated tests catch regressions before your users do, and a good debugging workflow gets you to the root cause fast when they don't. This chapter covers unit testing with xUnit/NUnit, mocking dependencies with Moq, integration testing, and the Visual Studio debugging tools — breakpoints, conditional breakpoints, and watch windows.",
            Keywords = new List<string> { "testing", "unit testing", "xunit", "nunit", "moq", "mocking", "integration testing", "debugging", "breakpoints", "watch window", "debug.assert", "fact", "theory", "assert" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Unit Testing (xUnit, NUnit)",
                    Content = "A unit test exercises a small piece of code — usually a single method or class — in isolation, and asserts that it behaves as expected. xUnit and NUnit are the two most widely used .NET test frameworks; both use attributes to mark test classes and methods and a fluent or classic Assert API to check outcomes. xUnit marks a no-argument test with `[Fact]` and a parameterized test with `[Theory]` plus one or more `[InlineData(...)]` rows.",
                    BulletPoints = new List<string>
                    {
                        "[Fact] — a single, fixed test case with no parameters.",
                        "[Theory] + [InlineData] — the same test logic run once per supplied set of arguments.",
                        "Arrange / Act / Assert is the conventional three-part shape of a test body: set up inputs, call the code under test, then check the result."
                    },
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Name tests descriptively, e.g. MethodName_Scenario_ExpectedBehavior — a failing test name should tell you most of what broke before you even open the file."
                },
                new()
                {
                    Heading = "Mocking (Moq)",
                    Content = "A unit test should exercise only the class under test, not its real dependencies (a database, an HTTP API, the clock). Moq lets you create a fake implementation of an interface at runtime, program canned return values with `Setup`, and later confirm which calls actually happened with `Verify`.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Mock interfaces (or virtual members), not concrete sealed classes — Moq generates a dynamic proxy that implements the interface / overrides the virtual members."
                },
                new()
                {
                    Heading = "Integration Testing",
                    Content = "Where unit tests isolate a single class, integration tests exercise several real collaborators together — for example a controller running through the actual ASP.NET Core pipeline with an in-memory or test database, verifying that the pieces are wired together correctly. They're slower and more brittle than unit tests but catch a class of bugs (wrong DI registration, serialization mismatches, real query behavior) that mocks can hide.",
                    BulletPoints = new List<string>
                    {
                        "ASP.NET Core's WebApplicationFactory<TEntryPoint> spins up an in-memory test server for a whole app.",
                        "Prefer a small number of high-value integration tests around critical flows, backed by a larger base of fast unit tests (the \"testing pyramid\").",
                        "Integration tests for data access often run against a real (or containerized) database rather than mocking it, since the SQL translation itself is part of what you're verifying."
                    }
                },
                new()
                {
                    Heading = "Debugging in Visual Studio",
                    Content = "The debugger lets you pause a running program and inspect its exact state instead of guessing from logs alone. Press F5 to start debugging, F9 to toggle a breakpoint on the current line, F10 to step over a line, F11 to step into a method call, and Shift+F11 to step out of the current method.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "The Exception Settings window (Debug > Windows > Exception Settings) lets you break the instant an exception is thrown, even one that's later caught — invaluable for chasing down a swallowed exception."
                },
                new()
                {
                    Heading = "Breakpoints & Watch Windows",
                    Content = "A plain breakpoint always stops execution when hit. A conditional breakpoint (right-click a breakpoint → Conditions) only stops when an expression you supply evaluates to true — essential for a bug that only reproduces on, say, the 500th loop iteration. The Watch window lets you type arbitrary expressions to evaluate and track while paused; Locals and Autos show variables already in scope without typing anything.",
                    BulletPoints = new List<string>
                    {
                        "Conditional breakpoint condition example: i == 499 inside a loop, to stop right before the failing iteration.",
                        "A hit-count condition (\"break after N hits\") is useful when you know roughly which iteration fails but not the exact index.",
                        "Debug.Assert(condition, message) is a lightweight, code-level alternative: it pops a debugger-break dialog in Debug builds when the condition is false, and compiles away entirely in Release builds."
                    }
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Assert.Equal", ReturnType = "void", Parameters = "T expected, T actual", Description = "xUnit: fails the test if actual does not equal expected, reporting both values in the failure message." },
                new() { MethodName = "Assert.Throws<TException>", ReturnType = "TException", Parameters = "Action testCode", Description = "xUnit: fails the test unless testCode throws exactly TException; returns the caught exception for further assertions." },
                new() { MethodName = "Mock<T>.Setup / Verify", ReturnType = "ISetup<T> / void", Parameters = "Expression<Action<T>> expression", Description = "Moq: Setup programs a fake member's behavior; Verify later asserts that a specific call was (or wasn't) made, optionally a specific number of times." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_testing_xunit_fact_theory",
                    Title = "xUnit — [Fact] and [Theory]",
                    Description = "A simple calculator class tested with a fixed [Fact] test and a parameterized [Theory] test. Assumes the xUnit NuGet package (xunit + xunit.runner.visualstudio); the test class itself won't execute directly in this app's script runner without a test host, but the logic and assertions are ordinary, compilable C#.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    public class Calculator
                    {
                        public int Add(int a, int b) => a + b;

                        public int Divide(int a, int b)
                        {
                            if (b == 0) throw new DivideByZeroException("Cannot divide by zero.");
                            return a / b;
                        }
                    }

                    public class CalculatorTests
                    {
                        private readonly Calculator _calculator = new();

                        [Fact]
                        public void Add_TwoPositiveNumbers_ReturnsSum()
                        {
                            int result = _calculator.Add(2, 3);
                            Assert.Equal(5, result);
                        }

                        [Theory]
                        [InlineData(10, 2, 5)]
                        [InlineData(9, 3, 3)]
                        [InlineData(-6, 2, -3)]
                        public void Divide_VariousInputs_ReturnsExpectedQuotient(int a, int b, int expected)
                        {
                            int result = _calculator.Divide(a, b);
                            Assert.Equal(expected, result);
                        }

                        [Fact]
                        public void Divide_ByZero_ThrowsDivideByZeroException()
                        {
                            Assert.Throws<DivideByZeroException>(() => _calculator.Divide(10, 0));
                        }
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_testing_moq",
                    Title = "Mocking a Dependency with Moq",
                    Description = "A notification service depends on an IEmailSender interface, mocked so the test never sends a real email. Assumes the Moq and xUnit NuGet packages; illustrative of test code — not runnable directly in this app's script runner without a test host.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    public interface IEmailSender
                    {
                        bool Send(string toAddress, string subject, string body);
                    }

                    public class OrderNotificationService
                    {
                        private readonly IEmailSender _emailSender;

                        public OrderNotificationService(IEmailSender emailSender) => _emailSender = emailSender;

                        public bool NotifyOrderShipped(string customerEmail, int orderId)
                        {
                            return _emailSender.Send(customerEmail, $"Order #{orderId} shipped", "Your order is on its way!");
                        }
                    }

                    public class OrderNotificationServiceTests
                    {
                        [Fact]
                        public void NotifyOrderShipped_SendsEmail_WithExpectedSubject()
                        {
                            // Arrange
                            var mockSender = new Mock<IEmailSender>();
                            mockSender
                                .Setup(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                .Returns(true);

                            var service = new OrderNotificationService(mockSender.Object);

                            // Act
                            bool result = service.NotifyOrderShipped("customer@example.com", 42);

                            // Assert
                            Assert.True(result);
                            mockSender.Verify(
                                s => s.Send("customer@example.com", "Order #42 shipped", It.IsAny<string>()),
                                Times.Once);
                        }
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_testing_debug_assert",
                    Title = "Debug.Assert — A Runnable Invariant Check",
                    Description = "Runs directly in this app's script runner: demonstrates using Debug.Assert to guard an invariant, plus where you'd typically drop a conditional breakpoint while stepping through the loop.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Diagnostics;

                    int[] scores = { 91, 88, 76, 64, 99, 55 };
                    int runningTotal = 0;

                    for (int i = 0; i < scores.Length; i++)
                    {
                        runningTotal += scores[i];

                        // In Visual Studio, you could right-click a breakpoint on the next line and set
                        // a condition like "i == 3" so execution only stops on that specific iteration.
                        Console.WriteLine($"After index {i}: running total = {runningTotal}");

                        // Debug.Assert checks an invariant while debugging; it's compiled out of Release builds,
                        // so it has zero runtime cost in production.
                        Debug.Assert(runningTotal >= 0, "Running total should never go negative for non-negative scores.");
                    }

                    double average = runningTotal / (double)scores.Length;
                    Console.WriteLine($"Final total: {runningTotal}, average: {average:F2}");
                    """
                }
            }
        };
    }
}
