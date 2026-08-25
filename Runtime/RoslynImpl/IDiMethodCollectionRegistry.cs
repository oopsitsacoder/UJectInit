using System.Collections.Generic;

namespace UJect.Init
{
    public interface IDiMethodCollectionRegistry
    {
        void CollectMethodCollections(HashSet<IDiBindMethodCollection> methodCollectionSet);
    }
}