﻿// Copyright 2003-2023 by Autodesk, Inc.
// 
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
// 
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
// 
// Use, duplication, or disclosure by the U.S. Government is subject to
// restrictions set forth in FAR 52.227-19 (Commercial Computer
// Software - Restricted Rights) and DFAR 252.227-7013(c)(1)(ii)
// (Rights in Technical Data and Computer Software), as applicable.

using System.Collections;
using System.Reflection;
using RevitLookup.Core.ComponentModel.Descriptors;
using RevitLookup.Core.Contracts;
using RevitLookup.Core.Objects;
using RevitLookup.Services.Contracts;

namespace RevitLookup.Core.Utils;

public sealed class DescriptorBuilder
{
    private readonly ISettingsService _settings;
    [CanBeNull] private Descriptor _currentDescriptor;
    private readonly SnoopableObject _snoopableObject;
    private readonly List<Descriptor> _descriptors;
    private Type _type;

    public DescriptorBuilder(SnoopableObject snoopableObject)
    {
        _snoopableObject = snoopableObject;
        _descriptors = new List<Descriptor>(8);
        _settings = Host.GetService<ISettingsService>();
    }

    private void AddProperties()
    {
        var members = _type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var descriptors = new List<Descriptor>(members.Length);

        foreach (var member in members)
        {
            if (member.IsSpecialName) continue;

            object value;
            ParameterInfo[] parameters = null;
            try
            {
                if (!TryEvaluate(member, out value, out parameters)) continue;
            }
            catch (Exception exception)
            {
                value = exception;
            }

            var descriptor = CreateMemberDescriptor(member, value, parameters);
            descriptors.Add(descriptor);
        }

        ApplyGroupCollector(descriptors);
    }

    private void AddMethods()
    {
        var members = _type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var descriptors = new List<Descriptor>(members.Length);

        foreach (var member in members)
        {
            if (member.IsSpecialName) continue;
            if (member.ReturnType.Name == "Void") continue;

            object value;
            ParameterInfo[] parameters = null;
            try
            {
                if (!TryEvaluate(member, out value, out parameters)) continue;
            }
            catch (Exception exception)
            {
                value = exception;
            }

            var descriptor = CreateMemberDescriptor(member, value, parameters);
            descriptors.Add(descriptor);
        }

        AddExtensions(descriptors);
        ApplyGroupCollector(descriptors);
    }

    private void AddExtensions(List<Descriptor> descriptors)
    {
        if (!_settings.IsExtensionsAllowed) return;
        if (_currentDescriptor is not IDescriptorExtension extension) return;
    }

    private void AddEnumerableItems()
    {
        if (_snoopableObject.Object is not IEnumerable enumerable) return;

        var enumerator = enumerable.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var enumerableDescriptor = new ObjectDescriptor
            {
                Type = nameof(IEnumerable),
                Value = new SnoopableObject(_snoopableObject.Context, enumerator.Current)
            };

            enumerableDescriptor.Label = enumerableDescriptor.Value.Descriptor.Type;
            _descriptors.Add(enumerableDescriptor);
        }
    }

    public IReadOnlyList<Descriptor> Build()
    {
        if (_snoopableObject.Object is null) return Array.Empty<Descriptor>();

        var type = _snoopableObject.Object.GetType();
        var types = new List<Type>();
        while (type.BaseType is not null)
        {
            types.Add(type);
            type = type.BaseType;
        }

        for (var i = types.Count - 1; i >= 0; i--)
        {
            _type = types[i];

            //Finding a descriptor to analyze IDescriptorResolver and IDescriptorExtension interfaces
            _currentDescriptor = DescriptorUtils.FindSuitableDescriptor(_snoopableObject.Object, _type);

            AddProperties();
            AddMethods();
        }
        
        AddEnumerableItems();

        return _descriptors;
    }

    private bool TryEvaluate(PropertyInfo member, out object value, out ParameterInfo[] parameters)
    {
        value = null;
        parameters = null;

        if (!member.CanRead)
        {
            value = new Exception("Property does not have a get accessor, it cannot be read");
            return true;
        }

        parameters = member.GetMethod.GetParameters();
        if (_currentDescriptor is IDescriptorResolver resolver)
        {
            value = resolver.Resolve(member.Name, parameters);
            if (value is not null) return true;
        }

        if (parameters.Length > 0)
        {
            if (_settings.IsUnsupportedAllowed)
            {
                value = new Exception("Unsupported property overload");
                return true;
            }

            return false;
        }

        value = member.GetValue(_snoopableObject.Object);
        return true;
    }

    private bool TryEvaluate(MethodInfo member, out object value, out ParameterInfo[] parameters)
    {
        parameters = member.GetParameters();
        if (_currentDescriptor is IDescriptorResolver resolver)
        {
            value = resolver.Resolve(member.Name, parameters);
            if (value is not null) return true;
        }

        if (parameters.Length > 0)
        {
            if (_settings.IsUnsupportedAllowed)
            {
                value = new Exception("Unsupported method overload");
                return true;
            }

            value = null;
            return false;
        }

        value = member.Invoke(_snoopableObject.Object, null);
        return true;
    }

    private void ApplyGroupCollector(List<Descriptor> descriptors)
    {
        descriptors.Sort();
        _descriptors.AddRange(descriptors);
    }

    private ObjectDescriptor CreateMemberDescriptor(MemberInfo member, object value, ParameterInfo[] parameters)
    {
        var descriptor = new ObjectDescriptor
        {
            Type = _currentDescriptor is null ? member.DeclaringType!.Name : _currentDescriptor.Type,
        };

        if (parameters is null || parameters.Length == 0)
        {
            descriptor.Label = member.Name;
        }
        else
        {
            descriptor.Label = $"{member.Name} ({string.Join(", ", parameters.Select(info => info.ParameterType.Name))})";
        }

        if (value is ResolveSummary summary)
        {
            if (summary.ResultCollection is null)
            {
                descriptor.Value = new SnoopableObject(_snoopableObject.Context, summary.Result);
                summary.UpdateLabel(descriptor.Value.Descriptor);
            }
            else
            {
                descriptor.Value = new SnoopableObject(_snoopableObject.Context, summary.ResultCollection);
                descriptor.Value.Descriptor.Label = member switch
                {
                    PropertyInfo property => DescriptorUtils.MakeGenericTypeName(property.GetMethod.ReturnType),
                    MethodInfo method => DescriptorUtils.MakeGenericTypeName(method.ReturnType),
                    _ => descriptor.Value.Descriptor.Label
                };
            }
        }
        else
        {
            descriptor.Value = new SnoopableObject(_snoopableObject.Context, value);
        }

        return descriptor;
    }
}