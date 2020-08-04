// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.ChangeTracking
{
    /// <summary>
    ///     <para>
    ///         Provides access to change tracking and loading information for a reference (i.e. non-collection)
    ///         navigation property that associates this entity to another entity.
    ///     </para>
    ///     <para>
    ///         Instances of this class are returned from methods when using the <see cref="ChangeTracker" /> API and it is
    ///         not designed to be directly constructed in your application code.
    ///     </para>
    /// </summary>
    public class ReferenceEntry : NavigationEntry
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        [EntityFrameworkInternal]
        public ReferenceEntry([NotNull] InternalEntityEntry internalEntry, [NotNull] string name)
            : base(internalEntry, name, collection: false)
        {
            LocalDetectChanges();

            // ReSharper disable once VirtualMemberCallInConstructor
            Check.DebugAssert(Metadata is INavigation, "Issue #21673. Non-collection skip navigations not supported.");
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        [EntityFrameworkInternal]
        public ReferenceEntry([NotNull] InternalEntityEntry internalEntry, [NotNull] INavigation navigation)
            : base(internalEntry, navigation)
        {
            LocalDetectChanges();

            // ReSharper disable once VirtualMemberCallInConstructor
            Check.DebugAssert(Metadata is INavigation, "Issue #21673. Non-collection skip navigations not supported.");
        }

        private void LocalDetectChanges()
        {
            if (!(Metadata is INavigation navigation
                && navigation.IsOnDependent))
            {
                var target = GetTargetEntry();
                if (target != null)
                {
                    var context = InternalEntry.StateManager.Context;
                    if (context.ChangeTracker.AutoDetectChangesEnabled
                        && (string)context.Model[CoreAnnotationNames.SkipDetectChangesAnnotation] != "true")
                    {
                        context.GetDependencies().ChangeDetector.DetectChanges(target);
                    }
                }
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether any of foreign key property values associated
        ///     with this navigation property have been modified and should be updated in the database
        ///     when <see cref="DbContext.SaveChanges()" /> is called.
        /// </summary>
        public override bool IsModified
        {
            get
            {
                var navigation = (INavigation)Metadata;

                return navigation.IsOnDependent
                    ? navigation.ForeignKey.Properties.Any(InternalEntry.IsModified)
                    : AnyFkPropertiesModified(navigation, CurrentValue);
            }
            set
            {
                var navigation = (INavigation)Metadata;

                if (navigation.IsOnDependent)
                {
                    SetFkPropertiesModified(navigation, InternalEntry, value);
                }
                else
                {
                    var navigationValue = CurrentValue;
                    if (navigationValue != null)
                    {
                        var relatedEntry = InternalEntry.StateManager.TryGetEntry(navigationValue, Metadata.TargetEntityType);
                        if (relatedEntry != null)
                        {
                            SetFkPropertiesModified(navigation, relatedEntry, value);
                        }
                    }
                }
            }
        }

        private  void SetFkPropertiesModified(
            INavigation navigation,
            InternalEntityEntry internalEntityEntry,
            bool modified)
        {
            var anyNonPk = navigation.ForeignKey.Properties.Any(p => !p.IsPrimaryKey());
            foreach (var property in navigation.ForeignKey.Properties)
            {
                if (anyNonPk
                    && !property.IsPrimaryKey())
                {
                    internalEntityEntry.SetPropertyModified(property, isModified: modified, acceptChanges: false);
                }
            }
        }

        private bool AnyFkPropertiesModified(INavigation navigation, object relatedEntity)
        {
            if (relatedEntity == null)
            {
                return false;
            }

            var relatedEntry = InternalEntry.StateManager.TryGetEntry(relatedEntity, Metadata.TargetEntityType);

            return relatedEntry != null
                && (relatedEntry.EntityState == EntityState.Added
                    || relatedEntry.EntityState == EntityState.Deleted
                    || navigation.ForeignKey.Properties.Any(relatedEntry.IsModified));
        }

        /// <summary>
        ///     The <see cref="EntityEntry" /> of the entity this navigation targets.
        /// </summary>
        /// <value> An entry for the entity that this navigation targets. </value>
        public virtual EntityEntry TargetEntry
        {
            get
            {
                var target = GetTargetEntry();
                return target == null ? null : new EntityEntry(target);
            }
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        [EntityFrameworkInternal]
        protected virtual InternalEntityEntry GetTargetEntry()
            => CurrentValue == null
                ? null
                : InternalEntry.StateManager.GetOrCreateEntry(CurrentValue, Metadata.TargetEntityType);
    }
}
