# SEION Formal Spec

## Status

SEION v0.1 is implemented as an advisory mathematical and KGE software layer inside EMA AI. It is not an official readiness, compliance, or evidence authority.

## Object

The minimal object is:

```text
SEION_Object = (V, K, mu, P, A)
```

- `V`: finite-dimensional real vector space, implemented as `R^D`.
- `K`: optional dense tensor with shape `(D, D, D, D)`.
- `mu`: ternary product.
- `P`: projector matrix.
- `A`: numerical associator/coherence defect.

## v0.1 Product

Default product:

```text
mu(x, y, z) = x * y * z
```

Optional structured form:

```text
mu(x, y, z) = W @ (x * y * z)
```

This default is labeled `simple_structured_kernel`. It is not an E8 kernel.

Dense kernels are accepted through the product interface when supplied and tested. E8-style kernels are future/pluggable only.

## Claims Boundary

SEION v0.1 computes numerical metrics and advisory rankings. It does not prove Hodge-theoretic statements, physical claims, compliance, or deliverable readiness.
