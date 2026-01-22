using QaaS.Framework.SDK.ContextObjects;

namespace QaaS.Framework.SDK.Extensions;

public static class ContextExtensions
{
    extension(InternalContext context)
    {
        public List<string> GetMetaDataPath()
        {
            var metaDataPath = new List<string>();
            if (context.CaseName != null)
                metaDataPath.Add(context.CaseName);
            if (context.ExecutionId != null)
                metaDataPath.Add(context.ExecutionId);
            metaDataPath.Add(nameof(MetaDataConfig));
            return metaDataPath;
        }

        public MetaDataConfig GetMetaDataFromContext() =>
            context.GetValueFromGlobalDictionary(GetMetaDataPath(context)) as MetaDataConfig ??
            throw new InvalidOperationException($"{nameof(MetaDataConfig)} was not found in Context");
    }
}