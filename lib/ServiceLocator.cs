using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Skatech.Components;

static class ServiceLocator {
    private static readonly Dictionary<Type, object> Services = new();
    private static readonly Dictionary<Type, Type> Classes = new();
    
    public static void Register<TService>(Type type) {
        if (typeof(TService).IsAssignableFrom(type)) {
            Classes[typeof(TService)] = type;
        }
        else throw new InvalidOperationException(
            $"Service type is not assignable from specified type: \"{typeof(TService)}\", \"{type}\".");
    }
    
    public static void Register<TService>(object service) {
        Register<TService>(service.GetType());
        Services[typeof(TService)] = service;
    }
    
    public static TService Resolve<TService>() {
        return (TService)Resolve(typeof(TService));
    }

    /*public static object? Resolve(Type type, bool throwNotRegistered = true) {
        if (Services.TryGetValue(type, out object? service)) {
            return service;
        }
        if (Classes.TryGetValue(type, out Type? restype)) {
            Services[type] = service = Create(restype);
            return service;
        }
        if (throwNotRegistered) {
            throw new InvalidOperationException($"Service is not registered: \"{type}\".");
        }
        return null;
    }*/
    
    public static object Resolve(Type type) {
        return TryResolve(type, out object? service) ? service
            : throw new InvalidOperationException($"Service is not registered: \"{type}\".");
    }
    
    public static bool TryResolve(Type type, [MaybeNullWhen(false)]out object service) {
        if (Services.TryGetValue(type, out service)) {
            return true;
        }
        if (Classes.TryGetValue(type, out Type? restype)) {
            Services[type] = service = Create(restype)!;
            return true;
        }
        return false;
    }

    static object? Create(Type type, bool throwCantCreate = true) {
        ConstructorInfo? ctor = default;
        ParameterInfo[]? args = default;
        int rate = 0;
        foreach(var c in type.GetConstructors(BindingFlags.Public|BindingFlags.Instance)) {
            var a = c.GetParameters();
            int r = a.Sum(p => Services.ContainsKey(p.ParameterType)
                ? 99 : Classes.ContainsKey(p.ParameterType)
                    ? 98 : p.HasDefaultValue ? 0 : -1000000);
            if (r >= rate) {
                rate = r;
                args = a;
                ctor = c;
            }
        }
        if (ctor != null) {
            return ctor.Invoke(args!.Select(
                p => TryResolve(p.ParameterType, out object? obj) ? obj : p.DefaultValue).ToArray());
        }
        return (throwCantCreate)
            ? throw new InvalidOperationException($"Unable to create instance of: \"{type}\".")
            : null;
    }
}
