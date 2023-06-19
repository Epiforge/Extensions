This library has useful tools for dealing with expressions:

* `ExpressionEqualityComparer` - Defines methods to support the comparison of expression trees for equality.
* `ExpressionExtensions`, providing:
  * `Duplicate` - Duplicates the specified expression tree.
  * `SubstituteMethods` - Recursively scans an expression tree to replace invocations of specific methods with replacement methods.