namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;

public static partial class ResultExtensions
{
    public static object GetValue(this Result source)
    {
        if (source == null)
        {
            return default;
        }

        var type = source.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DomainPolicyResult<>))
        {
            var property = type.GetProperty("Value");
            if (property != null)
            {
                return property.GetValue(source);
            }
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var property = type.GetProperty("Value");
            if (property != null)
            {
                return property.GetValue(source);
            }
        }

        return default;
    }
}
