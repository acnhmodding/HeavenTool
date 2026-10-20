using System.ComponentModel;

namespace HeavenTool.IO;

public sealed class ReadOnlyArrayConverter : ArrayConverter
{
    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object? value, Attribute[]? attributes) =>
        new([.. (base.GetProperties(context, value, attributes) ?? PropertyDescriptorCollection.Empty)
            .Cast<PropertyDescriptor>().Select(p => new ReadOnlyProperty(p))]);

    private sealed class ReadOnlyProperty(PropertyDescriptor property) : PropertyDescriptor(property, [ReadOnlyAttribute.Yes])
    {
        public override Type ComponentType => property.ComponentType;
        public override Type PropertyType => property.PropertyType;
        public override bool IsReadOnly => true;
        public override object? GetValue(object? component) => property.GetValue(component);
        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }
        public override void SetValue(object? component, object? value) { }
        public override bool ShouldSerializeValue(object component) => property.ShouldSerializeValue(component);
    }
}