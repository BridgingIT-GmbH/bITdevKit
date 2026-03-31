// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal static class PipelineContextTypeResolver
{
    public static Type InferStepContextType(Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        var contextType = FindClosedGenericBaseArgument(stepType, typeof(PipelineStep<>))
            ?? FindClosedGenericBaseArgument(stepType, typeof(AsyncPipelineStep<>));

        return contextType ?? throw new PipelineDefinitionValidationException(
            $"Pipeline step '{stepType.PrettyName()}' must derive from '{typeof(PipelineStep<>).PrettyName()}' or '{typeof(AsyncPipelineStep<>).PrettyName()}'.");
    }

    public static Type InferHookContextType(Type hookType)
    {
        ArgumentNullException.ThrowIfNull(hookType);

        return FindClosedGenericInterfaceArgument(hookType, typeof(IPipelineHook<>))
            ?? throw new PipelineDefinitionValidationException(
                $"Pipeline hook '{hookType.PrettyName()}' must implement '{typeof(IPipelineHook<>).PrettyName()}'.");
    }

    public static Type InferBehaviorContextType(Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);

        return FindClosedGenericInterfaceArgument(behaviorType, typeof(IPipelineBehavior<>))
            ?? throw new PipelineDefinitionValidationException(
                $"Pipeline behavior '{behaviorType.PrettyName()}' must implement '{typeof(IPipelineBehavior<>).PrettyName()}'.");
    }

    public static bool IsCompatible(Type pipelineContextType, Type componentContextType)
    {
        ArgumentNullException.ThrowIfNull(pipelineContextType);
        ArgumentNullException.ThrowIfNull(componentContextType);

        return componentContextType == typeof(NullPipelineContext) ||
            componentContextType.IsAssignableFrom(pipelineContextType);
    }

    private static Type FindClosedGenericBaseArgument(Type type, Type genericBaseTypeDefinition)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericBaseTypeDefinition)
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static Type FindClosedGenericInterfaceArgument(Type type, Type genericInterfaceDefinition)
    {
        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterfaceDefinition)
            ?.GetGenericArguments()[0];
    }
}
