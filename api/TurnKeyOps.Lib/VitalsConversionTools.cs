namespace MedInsights.Lib.Utils
{
    public static class VitalsConversionTools
    {
        private const decimal InchesPerCentimeter = 0.3937007874m;
        private const decimal PoundsPerKilogram = 2.2046226218m;

        public static decimal CelsiusToFahrenheit(decimal celsius, int? decimalPlaces = 2)
            => RoundOptional((celsius * 9m / 5m) + 32m, decimalPlaces);

        public static decimal FahrenheitToCelsius(decimal fahrenheit) => (fahrenheit - 32m) * 5m / 9m;

        public static decimal CentimetersToInches(decimal centimeters, int? decimalPlaces = 2)
            => RoundOptional(centimeters * InchesPerCentimeter, decimalPlaces);

        public static decimal InchesToCentimeters(decimal inches) => inches / InchesPerCentimeter;

        public static decimal KilogramsToPounds(decimal kilograms, int? decimalPlaces = 2)
            => RoundOptional(kilograms * PoundsPerKilogram, decimalPlaces);

        public static decimal PoundsToKilograms(decimal pounds) => pounds / PoundsPerKilogram;

        public static decimal? CalculateBmi(decimal? kilograms, decimal? centimeters, int? decimalPlaces = 2)
        {
            if (!kilograms.HasValue || !centimeters.HasValue || centimeters.Value <= 0m) return null;

            var meters = centimeters.Value / 100m;
            var bmi = kilograms.Value / (meters * meters);

            if (!decimalPlaces.HasValue) return bmi;
            if (decimalPlaces.Value < 0) throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "decimalPlaces cannot be negative.");

            return Math.Round(bmi, decimalPlaces.Value, MidpointRounding.AwayFromZero);
        }

        private static decimal RoundOptional(decimal value, int? decimalPlaces)
        {
            if (!decimalPlaces.HasValue) return value;
            if (decimalPlaces.Value < 0) throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "decimalPlaces cannot be negative.");

            return Math.Round(value, decimalPlaces.Value, MidpointRounding.AwayFromZero);
        }
    }
}
