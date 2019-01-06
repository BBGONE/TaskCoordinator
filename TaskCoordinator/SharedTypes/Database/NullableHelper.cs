using System;

namespace Shared.Database
{
    /// <summary>
    /// Вспомогательный класс для работы с Nullable-типами
    /// </summary>
    public static class NullableHelper
    {
        public static object DBNullConvertFrom<T>(Nullable<T> value) where T : struct
        {
            if (value.HasValue)
                return value.Value;
            else
                return System.DBNull.Value;
        }

        public static object DBNullConvertFrom<T>(T value) where T : class
        {
            if (value != null)
                return value;
            else
                return System.DBNull.Value;
        }

        public static Nullable<T> DBNullConvertNullableTo<T>(object value) where T : struct
        {
            if (value == null || value == System.DBNull.Value)
                return null;
            else
                return (T)value;
        }

        public static T DBNullConvertTo<T>(object value) where T : class
        {
            if (value == null || value == System.DBNull.Value)
                return null;
            else
                return (T)value;
        }

        /// <summary>
        /// Возвращает null, если исходная строка пустая или null. 
        /// В противном случае возвращает исходную строку.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string NullIfEmpty(string str)
        {
            return string.IsNullOrEmpty(str) ? null : str;
        }

        /// <summary>
        /// Возвращает null, если исходная строка null или Trim исходной строки - пустая строка.
        /// В противном случае возвращает Trim исходной строки.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string NullIfTrimEmpty(string str)
        {
            string trimmed = str == null ? null : str.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        /// <summary>
        /// Возвращает true, если исходная строка null или Trim исходной строки - пустая строка
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNullOrTrimEmpty(string str)
        {
            if (str == null)
                return true;
            else
                return str.Trim() == string.Empty;
        }
    }
}
