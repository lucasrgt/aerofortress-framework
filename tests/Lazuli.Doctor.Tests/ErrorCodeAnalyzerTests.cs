namespace Lazuli.Doctor.Tests;

public class ErrorCodeAnalyzerTests
{
    [Fact]
    public Task Registry_constant_and_codeless_calls_report_nothing() =>
        Harness<ErrorCodeAnalyzer>.Verify(Code("""
            Error.NotFound(WalletsErrorCodes.NotFound, "not found");
            new Validation().Check(true, "amount", WalletsErrorCodes.NotFound, "bad");
            new Validation().Collect("amount", 0);
            Error.Validation(new List<int>());
            """));

    [Fact]
    public Task Inline_literal_in_a_factory_is_flagged() =>
        Harness<ErrorCodeAnalyzer>.Verify(Code("""
            Error.NotFound({|LZ0018:"wallets.not_found"|}, "not found");
            """));

    [Fact]
    public Task Inline_literal_in_a_check_is_flagged() =>
        Harness<ErrorCodeAnalyzer>.Verify(Code("""
            new Validation().Check(true, "amount", {|LZ0018:"amount.required"|}, "is required");
            """));

    [Fact]
    public Task Inline_literal_in_a_field_error_is_flagged() =>
        Harness<ErrorCodeAnalyzer>.Verify(Code("""
            var _ = new FieldError("amount", {|LZ0018:"amount.required"|}, "is required");
            """));

    // Stubs mirror the real shapes: Error factories + Validation.Check/Add + the FieldError record each declare a
    // 'code' parameter; Collect and the aggregate Error.Validation(fields) do not, so they are left alone. A code
    // is conformant only when it references a const on a class whose name ends with ErrorCodes.
    private static string Code(string body) => $$"""
        using System.Collections.Generic;

        public static class Subject
        {
            public static void M()
            {
                {{body}}
            }
        }

        public static class WalletsErrorCodes { public const string NotFound = "wallets.not_found"; }

        public sealed class Error
        {
            public static Error NotFound(string code, string message) => new();
            public static Error Validation(IReadOnlyList<int> fields) => new();
        }

        public sealed class Validation
        {
            public Validation Check(bool ok, string field, string code, string message) => this;
            public Validation Add(string field, string code, string message) => this;
            public Validation Collect(string field, int result) => this;
        }

        public readonly record struct FieldError(string Field, string Code, string Message);
        """;
}
