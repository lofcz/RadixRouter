using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Memory;

namespace BlazingRouter.Services;

internal static class RouterService
{
    class RouteParam
    {
        public string Name { get; set; }
        public Type Type { get; set; }
    }

    public static IMemoryCache Cache;
    private static readonly Dictionary<string, object?> EmptyKvDict = new Dictionary<string, object?>();
    private static readonly Tuple<bool, Dictionary<string, object?>> EmptyParamMap = new Tuple<bool, Dictionary<string, object?>>(true, EmptyKvDict);

    private static PropertyInfo[] GetTypeProperties(Type type)
    {
        if (Cache.TryGetValue($"router_type_pars_{type}", out PropertyInfo[]? cached) && cached is not null)
        {
            return cached;
        }
        
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        return properties;
    }
    
    private static List<RouteParam>? GetRouteMappedPars(Route? route)
    {
        if (route is null)
        {
            return null;
        }
        
        if (Cache.TryGetValue($"router_route_pars_{route.Id}", out List<RouteParam>? cached))
        {
            return cached;
        }

        PropertyInfo[] props = GetTypeProperties(route.Handler);
        List<RouteParam> mappedPars = [];

        List<RouteSegment> attrPars = route.Segments.Where(x => x.Type is RouteSegmentTypes.Dynamic).ToList();
        
        
        foreach (RouteSegment attrPar in attrPars)
        {
            PropertyInfo? matchingProperty = props.FirstOrDefault(x => string.Equals(x.Name, attrPar.LiteralValue, StringComparison.InvariantCultureIgnoreCase));

            if (matchingProperty is null)
            {
                // matching prop not found, bail
                continue;
            }
            
            mappedPars.Add(new RouteParam {Name = attrPar.LiteralValue, Type = matchingProperty.PropertyType});
        }

        Cache.Set($"router_route_pars_{route.Id}", mappedPars, DateTime.MaxValue);
        return mappedPars;
    }
    
    public static Tuple<bool, Dictionary<string, object?>> MapUrlParams(Route? route, Dictionary<string, string>? pars)
    {
        if (route is null)
        {
            return EmptyParamMap;
        }
        
        List<RouteParam>? map = GetRouteMappedPars(route);

        if (map is null)
        {
            return EmptyParamMap;
        }

        Dictionary<string, object?> mapped = [];

        if (pars is not null)
        {
            foreach (RouteParam pair in map)
            {
                if (pars.TryGetValue(pair.Name, out string? val))
                {
                    if (typeof(string) == pair.Type)
                    {
                        mapped.Add(pair.Name, val);   
                    }
                    else
                    {
                        try
                        {
                            mapped.Add(pair.Name, val.ChangeType(pair.Type));
                        }
                        catch (Exception e)
                        {
                            return new Tuple<bool, Dictionary<string, object?>>(false, mapped);
                        }
                    }
                }
            }   
        }

        return new Tuple<bool, Dictionary<string, object?>>(true, mapped);
    }

    public static Dictionary<string, object?>? FilterQueryParams(Type? type, Dictionary<string, object?>? pars)
    {
        if (type is null || pars is null || pars.Count is 0)
        {
            return pars;
        }
        
        Dictionary<string, RouteParam> properties;
        
        if (Cache.TryGetValue($"router_type_{type.FullName}", out object? info) && info is Dictionary<string, RouteParam> pi)
        {
            properties = pi;
        }
        else
        {
            properties = [];
            
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                string paramName = prop.Name.ToLowerInvariant();
                SupplyParameterFromQueryAttribute? queryParamAttr = prop.GetCustomAttribute<SupplyParameterFromQueryAttribute>();
                ParameterAttribute? paramAttr = prop.GetCustomAttribute<ParameterAttribute>();
                CascadingParameterAttribute? cascadingParamAttr = prop.GetCustomAttribute<CascadingParameterAttribute>();
                SupplyParameterFromFormAttribute? formParamAttr = prop.GetCustomAttribute<SupplyParameterFromFormAttribute>();
                
                // any cascading or cascading-derived params must be filtered
                if (queryParamAttr is not null || formParamAttr is not null || cascadingParamAttr is not null)
                {
                    continue;
                }
                
                if (paramAttr is not null)
                {
                    properties[paramName] = new RouteParam
                    {
                        Name = paramName,
                        Type = prop.PropertyType
                    };
                }
            }
                
            Cache.Forever($"router_type_{type.FullName}", properties);
        }
        
        List<KeyValuePair<string, object?>> itemsToRemove = pars.Where(x => !properties.ContainsKey(x.Key)).ToList();
        
        #if BLAZING_ROUTER_VERBOSE
        List<KeyValuePair<string, object?>>? ignored = null;
        #endif
        
        foreach (KeyValuePair<string, object?> item in itemsToRemove)
        {
            pars.Remove(item.Key);
            
            #if BLAZING_ROUTER_VERBOSE
            ignored ??= [];
            ignored.Add(item);
            #endif
        }
        
        #if BLAZING_ROUTER_VERBOSE
        if (ignored?.Count > 0)
        {
            Debug.WriteLine("--- Some arguments were ignored when resolving the route ---");

            foreach (KeyValuePair<string, object?> itm in ignored)
            {
                Debug.WriteLine($"{itm.Key} = {itm.Value}");
            }
        }
        #endif
        
        foreach (KeyValuePair<string, object?> par in pars)
        {
            if (!properties.TryGetValue(par.Key, out RouteParam? rp))
            {
                continue;
            }

            object? value;

            if (par.Value?.GetType() == rp.Type)
            {
                value = par.Value;
            }
            else
            {
                try
                {
                    value = par.Value.ChangeType(rp.Type);
                }
                catch (Exception)
                {
                    value = rp.Type.GetDefaultValue();
                }   
            }

            pars[par.Key] = value;
        }
        
        return pars;
    }
}