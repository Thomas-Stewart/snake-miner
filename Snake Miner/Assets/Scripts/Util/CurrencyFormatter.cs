using System;
using System.Globalization;

public static class CurrencyFormatter
{
	private const long ShortFormatThreshold = 100_000L;
	private static readonly string[] Suffixes = { "K", "M", "B", "T", "Q" };

	public static string GetNumberShortText(long number, bool showMoreDecimals = true)
	{
		if (number > -ShortFormatThreshold && number < ShortFormatThreshold)
			return number.ToString(CultureInfo.InvariantCulture);

		return FormatShortNumber(number, showMoreDecimals);
	}

	public static string GetNumberShortText(double number, bool intify = true, bool showMoreDecimals = true)
	{
		if (double.IsNaN(number) || double.IsInfinity(number))
			return number.ToString(CultureInfo.InvariantCulture);

		if (Math.Abs(number) < ShortFormatThreshold)
		{
			return intify
				? ((long)number).ToString(CultureInfo.InvariantCulture)
				: number.ToString(CultureInfo.InvariantCulture);
		}

		return FormatShortNumber(number, showMoreDecimals);
	}

	private static string FormatShortNumber(double number, bool showMoreDecimals)
	{
		var absoluteNumber = Math.Abs(number);
		var suffixIndex = -1;

		while (absoluteNumber >= 1000d && suffixIndex < Suffixes.Length - 1)
		{
			absoluteNumber /= 1000d;
			suffixIndex++;
		}

		if (suffixIndex == Suffixes.Length - 1 && absoluteNumber >= 1000d)
			return FormatScientific(number);

		var decimalPlaces = showMoreDecimals ? 2 : 1;
		var roundedNumber = Math.Round(absoluteNumber, decimalPlaces, MidpointRounding.AwayFromZero);
		if (number < 0d)
			roundedNumber = -roundedNumber;

		var numberFormat = showMoreDecimals ? "0.##" : "0.#";
		return roundedNumber.ToString(numberFormat, CultureInfo.InvariantCulture) + Suffixes[suffixIndex];
	}

	private static string FormatScientific(double number)
	{
		var absoluteNumber = Math.Abs(number);
		var exponent = (int)Math.Floor(Math.Log10(absoluteNumber));
		var mantissa = number / Math.Pow(10d, exponent);
		return mantissa.ToString("0.00", CultureInfo.InvariantCulture) + "e" + exponent.ToString(CultureInfo.InvariantCulture);
	}
}
