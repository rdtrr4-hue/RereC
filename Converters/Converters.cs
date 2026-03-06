using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Rere.Models;

namespace Rere.Converters
{
    // ─── Bool → Connect Button Color (green/red dot) ─────────────────────────────
    public class BoolToConnectColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => (bool)v
                ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
                : new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // ─── Bool → "اتصال" / "قطع الاتصال" ────────────────────────────────────────
    public class BoolToConnectTextConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => (bool)v ? "قطع الاتصال" : "اتصال";

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // ─── LogType → Color ─────────────────────────────────────────────────────────
    public class LogTypeToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => v is ActivityLog.LogType lt
                ? lt == ActivityLog.LogType.Join
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
                    : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))
                : Brushes.Gray;

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // ─── TRXStatus → Color ───────────────────────────────────────────────────────
    public class TRXStatusToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            if (v is TRXEntry.TRXStatus s)
                return s switch
                {
                    TRXEntry.TRXStatus.Pending => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                    TRXEntry.TRXStatus.Sending => new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)),
                    TRXEntry.TRXStatus.Active  => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                    TRXEntry.TRXStatus.Success => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                    TRXEntry.TRXStatus.Failed  => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    _ => Brushes.Gray
                };
            return Brushes.Gray;
        }

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // ─── TRXVerdict → Color ──────────────────────────────────────────────────────
    public class VerdictToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            if (v is TRXLogEntry.TRXVerdict vd)
                return vd switch
                {
                    TRXLogEntry.TRXVerdict.Pending => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                    TRXLogEntry.TRXVerdict.Exited  => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                    TRXLogEntry.TRXVerdict.Stayed  => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    _ => Brushes.Gray
                };

            if (v is TRXLogEntry.TRXResult r)
                return r == TRXLogEntry.TRXResult.Success
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
                    : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

            return Brushes.Gray;
        }

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // ─── ToastType → Color ───────────────────────────────────────────────────────
    public class ToastTypeToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            if (v is ToastType tt)
                return tt switch
                {
                    ToastType.Success => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                    ToastType.Error   => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    ToastType.Warning => new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)),
                    ToastType.Info    => new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                    _ => Brushes.White
                };
            return Brushes.White;
        }

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // ─── Null → Visibility ───────────────────────────────────────────────────────
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => v == null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // ─── Inverse Bool ────────────────────────────────────────────────────────────
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => v is bool b ? !b : v;

        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => v is bool b ? !b : v;
    }

    // ─── Active IP → Color (green if active) ─────────────────────────────────────
    public class ActiveIPToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            var activeIP  = AppState.Shared.TrxActiveIP;
            var sessionIP = v as string;
            return !string.IsNullOrEmpty(activeIP) && activeIP == sessionIP
                ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
                : new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
        }

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    // ─── Countdown text ──────────────────────────────────────────────────────────
    public class CountdownTextConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => v is int i && i > 0 ? $"{i}s" : "";

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
