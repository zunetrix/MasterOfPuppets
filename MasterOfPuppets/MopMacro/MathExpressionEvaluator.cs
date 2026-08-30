using System;
using System.Globalization;

namespace MasterOfPuppets;

/// <summary>
/// Evaluates a decimal arithmetic expression string (e.g. "7 * 0.8", "(3 + 1) * 2.5").
/// Supports +, -, *, /, %, ^, parentheses and unary minus, with standard operator
/// precedence. Parsing is culture-invariant. <see cref="TryEvaluate"/> returns true
/// and the computed value only when the entire trimmed input is a finite arithmetic
/// expression; otherwise (e.g. "/clap", "some name") it returns false.
/// </summary>
public static class MathExpressionEvaluator {

    // Defensive limits: arithmetic here is always numeric-only (no identifiers,
    // functions or reflection), so the only realistic abuse vectors are pathological
    // input length and deeply nested parentheses/unary chains causing stack overflow.
    // Both are bounded below and treated as "not an expression" rather than evaluated.
    private const int MaxInputLength = 512;
    private const int MaxDepth = 64;

    /// <summary>
    /// Attempts to evaluate <paramref name="input"/> as an arithmetic expression.
    /// Returns true and the computed value on success; false when the input is not
    /// a finite arithmetic expression (non-numeric tokens, malformed syntax, overly
    /// long or overly deeply nested input, etc.).
    /// </summary>
    public static bool TryEvaluate(string? input, out string result) {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (trimmed.Length > MaxInputLength)
            return false;

        int pos = 0;
        try {
            double value = ParseExpression(trimmed, ref pos, 0);
            if (!double.IsFinite(value))
                return false;
            if (pos != trimmed.Length)
                return false; // trailing (non-whitespace) content -> not a pure expression

            result = FormatValue(value);
            return true;
        } catch {
            return false;
        }
    }

    private static string FormatValue(double value) {
        // Round to two decimals to match the plugin's wait precision, trimming trailing zeros.
        double rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        string text = rounded.ToString("0.##", CultureInfo.InvariantCulture);
        return text;
    }

    // Grammar (precedence climbing):
    //   expression   := additive
    //   additive     := multiplicative (('+' | '-') multiplicative)*
    //   multiplicative:= unary (('*' | '/' | '%') unary)*
    //   unary        := ('-' | '+') unary | power
    //   power        := primary ('^' unary)?
    //   primary      := number | '(' expression ')'
    private static double ParseExpression(string input, ref int pos, int depth) {
        return ParseAdditive(input, ref pos, depth);
    }

    private static void EnterDepth(int depth) {
        if (depth > MaxDepth)
            throw new FormatException("Arithmetic expression nesting too deep.");
    }

    private static double ParseAdditive(string input, ref int pos, int depth) {
        EnterDepth(depth);
        double value = ParseMultiplicative(input, ref pos, depth);
        while (true) {
            SkipWhitespace(input, ref pos);
            if (pos >= input.Length)
                break;
            char op = input[pos];
            if (op != '+' && op != '-')
                break;
            pos++;
            double rhs = ParseMultiplicative(input, ref pos, depth);
            value = op == '+' ? value + rhs : value - rhs;
        }
        return value;
    }

    private static double ParseMultiplicative(string input, ref int pos, int depth) {
        double value = ParseUnary(input, ref pos, depth);
        while (true) {
            SkipWhitespace(input, ref pos);
            if (pos >= input.Length)
                break;
            char op = input[pos];
            if (op != '*' && op != '/' && op != '%')
                break;
            pos++;
            double rhs = ParseUnary(input, ref pos, depth);
            value = op switch {
                '*' => value * rhs,
                '/' => value / rhs,
                _ => value % rhs,
            };
        }
        return value;
    }

    private static double ParseUnary(string input, ref int pos, int depth) {
        EnterDepth(depth);
        SkipWhitespace(input, ref pos);
        if (pos < input.Length && (input[pos] == '-' || input[pos] == '+')) {
            char sign = input[pos];
            pos++;
            double operand = ParseUnary(input, ref pos, depth + 1);
            return sign == '-' ? -operand : operand;
        }
        return ParsePower(input, ref pos, depth);
    }

    private static double ParsePower(string input, ref int pos, int depth) {
        double baseValue = ParsePrimary(input, ref pos, depth);
        SkipWhitespace(input, ref pos);
        if (pos < input.Length && input[pos] == '^') {
            pos++;
            double exponent = ParseUnary(input, ref pos, depth + 1);
            return Math.Pow(baseValue, exponent);
        }
        return baseValue;
    }

    private static double ParsePrimary(string input, ref int pos, int depth) {
        EnterDepth(depth);
        SkipWhitespace(input, ref pos);
        if (pos >= input.Length)
            throw new FormatException("Unexpected end of arithmetic expression.");

        char c = input[pos];
        if (c == '(') {
            pos++;
            double value = ParseExpression(input, ref pos, depth + 1);
            SkipWhitespace(input, ref pos);
            if (pos >= input.Length || input[pos] != ')')
                throw new FormatException("Unbalanced parentheses in arithmetic expression.");
            pos++;
            return value;
        }

        if (c is >= '0' and <= '9' || c == '.') {
            return ParseNumber(input, ref pos);
        }

        throw new FormatException($"Unexpected character '{c}' in arithmetic expression.");
    }

    private static double ParseNumber(string input, ref int pos) {
        int start = pos;
        while (pos < input.Length &&
               ((input[pos] >= '0' && input[pos] <= '9') || input[pos] == '.')) {
            pos++;
        }
        string token = input[start..pos];
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            throw new FormatException($"Invalid number '{token}' in arithmetic expression.");
        return value;
    }

    private static void SkipWhitespace(string input, ref int pos) {
        while (pos < input.Length && char.IsWhiteSpace(input[pos]))
            pos++;
    }
}
