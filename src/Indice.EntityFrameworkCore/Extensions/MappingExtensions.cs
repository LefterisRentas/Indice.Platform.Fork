﻿using Indice.EntityFrameworkCore.ValueConversion;

namespace Microsoft.EntityFrameworkCore.Metadata.Builders
{
    /// <summary>
    /// Extension methods on <see cref="PropertyBuilder{TProperty}"/>.
    /// </summary>
    public static class MappingExtensions
    {
        /// <summary>
        /// Configures the property so that the property value is converted to and from the database using the <see cref="JsonStringValueConverter{T}"/>.
        /// </summary>
        /// <typeparam name="TProperty">The store type generated by the converter.</typeparam>
        /// <param name="builder">The builder instance.</param>
        /// <returns>The same builder instance so that multiple configuration calls can be chained.</returns>
        public static PropertyBuilder HasJsonConversion<TProperty>(this PropertyBuilder<TProperty> builder) where TProperty : class {
            builder.HasConversion(new JsonStringValueConverter<TProperty>());
            return builder;
        }
    }
}