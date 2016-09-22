using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BooleanSwitch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(bool),
                typeof(BooleanSwitch), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool Value {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty TrueProperty = DependencyProperty.Register(nameof(True), typeof(UIElement),
                typeof(BooleanSwitch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public UIElement True {
            get { return (UIElement)GetValue(TrueProperty); }
            set { SetValue(TrueProperty, value); }
        }

        public static readonly DependencyProperty FalseProperty = DependencyProperty.Register(nameof(False), typeof(UIElement),
                typeof(BooleanSwitch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public UIElement False {
            get { return (UIElement)GetValue(FalseProperty); }
            set { SetValue(FalseProperty, value); }
        }

        protected override UIElement GetChild() {
            return Value ? True : False;
        }
    }

    public class ReferenceSwitch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object),
                typeof(ReferenceSwitch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public object Value {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty NullProperty = DependencyProperty.Register(nameof(Null), typeof(UIElement),
                typeof(ReferenceSwitch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public UIElement Null {
            get { return (UIElement)GetValue(NullProperty); }
            set { SetValue(NullProperty, value); }
        }

        public static readonly DependencyProperty NonNullProperty = DependencyProperty.Register(nameof(NonNull), typeof(UIElement),
                typeof(ReferenceSwitch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public UIElement NonNull {
            get { return (UIElement)GetValue(NonNullProperty); }
            set { SetValue(NonNullProperty, value); }
        }

        protected override UIElement GetChild() {
            return Value == null ? Null : NonNull;
        }
    }
}