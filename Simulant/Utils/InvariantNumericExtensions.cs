using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Simulant.Utils
{
    public static class InvariantNumericExtensions
    {

        #region sbyte
        /// <summary>
        /// Convert an <see cref="sbyte"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this sbyte result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert an <see cref="sbyte"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this sbyte result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="sbyte"/>. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static sbyte ParseSByte(this string s)
            => sbyte.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="sbyte"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static sbyte ParseSByteWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseSByte(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="sbyte"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="sbyte"/>, or <see langword="null" /> if failed. </returns>
        public static sbyte? TryParseSByte(this string s)
            => sbyte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (sbyte?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="sbyte"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseSByte(this string s, out sbyte result)
            => sbyte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        #endregion sbyte
        #region byte
        /// <summary>
        /// Convert a <see cref="byte"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this byte result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="byte"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this byte result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="byte"/>. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static byte ParseByte(this string s)
            => byte.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="byte"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static byte ParseByteWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseByte(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="byte"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="byte"/>, or <see langword="null" /> if failed. </returns>
        public static byte? TryParseByte(this string s)
            => byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (byte?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="byte"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseByte(this string s, out byte result)
            => byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        #endregion byte
        #region short
        /// <summary>
        /// Convert a <see cref="short"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this short result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="short"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this short result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="short"/>. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static short ParseShort(this string s)
            => short.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="short"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static short ParseShortWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseShort(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="short"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="short"/>, or <see langword="null" /> if failed. </returns>
        public static short? TryParseShort(this string s)
            => short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (short?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="short"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseShort(this string s, out short result)
            => short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        #endregion short
        #region ushort
        /// <summary>
        /// Convert a <see cref="ushort"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this ushort result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="ushort"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this ushort result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="ushort"/>. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static ushort ParseUShort(this string s)
            => ushort.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="ushort"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static ushort ParseUShortWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseUShort(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="ushort"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="ushort"/>, or <see langword="null" /> if failed. </returns>
        public static ushort? TryParseUShort(this string s)
            => ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (ushort?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="ushort"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseUShort(this string s, out ushort result)
            => ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        #endregion ushort
        #region int
        /// <summary>
        /// Convert an <see cref="int"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this int result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert an <see cref="int"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this int result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="int"/>. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static int ParseInt(this string s)
            => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="int"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static int ParseIntWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseInt(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="int"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="int"/>, or <see langword="null" /> if failed. </returns>
        public static int? TryParseInt(this string s)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (int?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="int"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseInt(this string s, out int result)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        #endregion int
        #region uint
        /// <summary>
        /// Convert an <see cref="uint"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this uint result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert an <see cref="uint"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this uint result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="uint"/>. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static uint ParseUInt(this string s)
            => uint.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="uint"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static uint ParseUIntWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseUInt(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="uint"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="uint"/>, or <see langword="null" /> if failed. </returns>
        public static uint? TryParseUInt(this string s)
            => uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (uint?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="uint"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseUInt(this string s, out uint result)
            => uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        #endregion uint
        #region long
        /// <summary>
        /// Convert a <see cref="long"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this long result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="long"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this long result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="long"/>. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static long ParseLong(this string s)
            => long.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="long"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static long ParseLongWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseLong(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="long"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="long"/>, or <see langword="null" /> if failed. </returns>
        public static long? TryParseLong(this string s)
            => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (long?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="long"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseLong(this string s, out long result)
            => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        #endregion long
        #region ulong
        /// <summary>
        /// Convert an <see cref="ulong"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this ulong result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert an <see cref="ulong"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this ulong result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="ulong"/>. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static ulong ParseULong(this string s)
            => ulong.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to an <see cref="ulong"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static ulong ParseULongWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseULong(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="ulong"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="ulong"/>, or <see langword="null" /> if failed. </returns>
        public static ulong? TryParseULong(this string s)
            => ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (ulong?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to an <see cref="ulong"/>.  <br />
        /// Always use <see cref="NumberStyles.Integer"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseULong(this string s, out ulong result)
            => ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        #endregion ulong
        #region float
        /// <summary>
        /// Convert a <see cref="float"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this float result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="float"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this float result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="float"/>. <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static float ParseFloat(this string s)
            => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="float"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static float ParseFloatWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseFloat(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="float"/>.  <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="float"/>, or <see langword="null" /> if failed. </returns>
        public static float? TryParseFloat(this string s)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : (float?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="float"/>.  <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseFloat(this string s, out float result)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        #endregion float
        #region double
        /// <summary>
        /// Convert a <see cref="double"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this double result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="double"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this double result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="double"/>. <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static double ParseDouble(this string s)
            => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="double"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static double ParseDoubleWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseDouble(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="double"/>.  <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="double"/>, or <see langword="null" /> if failed. </returns>
        public static double? TryParseDouble(this string s)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : (double?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="double"/>.  <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseDouble(this string s, out double result)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        #endregion double
        #region decimal
        /// <summary>
        /// Convert a <see cref="decimal"/> to a <see cref="string"/>. <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this decimal result)
            => result.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="decimal"/> to a <see cref="string"/> with the specified format.  <br />
        /// Always use <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static string ToStringInvariant(this decimal result, string format)
            => result.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="decimal"/>. <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static decimal ParseDecimal(this string s)
            => decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="string"/> to a <see cref="decimal"/>. <br />
        /// If failed, throw an exception with detailed context information. <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static decimal ParseDecimalWithDetail(this string s, string contextDescription = null, string rawExpression = null)
            => s.ParseWithDetail(_s => _s.ParseDecimal(), contextDescription, rawExpression);

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="decimal"/>.  <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        /// <returns> The parsed <see cref="decimal"/>, or <see langword="null" /> if failed. </returns>
        public static decimal? TryParseDecimal(this string s)
            => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : (decimal?)null;

        /// <summary>
        /// Attempt to convert a <see cref="string"/> to a <see cref="decimal"/>.  <br />
        /// Always use <see cref="NumberStyles.Float"/> and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseDecimal(this string s, out decimal result)
            => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        #endregion decimal
        #region Throw with Detailed Message
        /// <summary>
        /// Parse a value using the provided parser and attach detailed context on failure. <br />
        /// · <paramref name="input" />: Raw text to parse. Must not be <see langword="null" />. <br />
        /// · <paramref name="parser" />: Delegate that performs the actual parse. Must not be <see langword="null" />. <br />
        /// · <paramref name="contextDescription" />: Human-readable description of where this value comes from (optional). <br />
        /// · <paramref name="rawExpression" />: Original expression containing the parsed string (optional).
        /// </summary>
        public static T ParseWithDetail<T>(this string input, Func<string, T> parser, string contextDescription = null, string rawExpression = null)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            try
            {
                return parser(input);
            }
            catch (Exception ex) // override the original message and rethrow
            {
                var message = BuildParseErrorMessage<T>(ex, input, contextDescription, rawExpression);
                OverrideExceptionMessage(message, ex);
                throw;
            }
        }

        private static string BuildParseErrorMessage<T>(Exception ex, string input, string contextDescription, string rawExpression)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Failed to parse '{input}' as {typeof(T).Name}: {ex.Message}");

            if (!string.IsNullOrWhiteSpace(contextDescription))
                sb.AppendLine($"Context: {contextDescription}");

            if (rawExpression != null) // allow empty since it could be the user input
                sb.AppendLine($"Expression: '{rawExpression}'");

            return sb.ToString();
        }

        private static readonly Dictionary<Type, FieldInfo> _messageFieldCache = new Dictionary<Type, FieldInfo>();

        static void OverrideExceptionMessage(string newMessage, Exception ex)
        {
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            var type = ex.GetType();
            FieldInfo field;

            lock (_messageFieldCache)
            {
                if (!_messageFieldCache.TryGetValue(type, out field))
                {
                    field = FindExceptionMessageField(type);
                    _messageFieldCache[type] = field;
                }
            }

            if (field != null)
                field.SetValue(ex, newMessage);

            else
                throw new Exception($"{ex.GetType().Name}: {newMessage}", ex);
        }

        private static FieldInfo FindExceptionMessageField(Type type)
        {
            while (type != null)
            {
                var field =
                    type.GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? type.GetField("m_message", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? type.GetField("message", BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null)
                    return field;

                type = type.BaseType;
            }

            return null;
        }

        #endregion
    }
}
