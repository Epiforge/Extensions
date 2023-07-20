using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionCountQuery<TElement> :
    ObservableCollectionScalarQuery<TElement, int>
{
    public ObservableCollectionCountQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery) :
        base(collectionObserver, observableCollectionQuery)
    {
    }

    protected override bool Dispose(bool disposing) => throw new NotImplementedException();

    protected override void OnInitialization() => throw new NotImplementedException();
}
