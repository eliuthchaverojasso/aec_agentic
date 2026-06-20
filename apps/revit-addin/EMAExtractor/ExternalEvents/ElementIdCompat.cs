using System;
using System.Reflection;
using Autodesk.Revit.DB;

namespace EMAExtractor.ExternalEvents
{
    internal static class ElementIdCompat
    {
        private static readonly ConstructorInfo LongConstructor = typeof(ElementId).GetConstructor(new[] { typeof(long) });
        private static readonly ConstructorInfo IntConstructor = typeof(ElementId).GetConstructor(new[] { typeof(int) });

        public static ElementId Create(int value)
        {
            if (LongConstructor != null)
            {
                return (ElementId)LongConstructor.Invoke(new object[] { (long)value });
            }

            if (IntConstructor != null)
            {
                return (ElementId)IntConstructor.Invoke(new object[] { value });
            }

            throw new InvalidOperationException("No compatible ElementId constructor was found.");
        }
    }
}
