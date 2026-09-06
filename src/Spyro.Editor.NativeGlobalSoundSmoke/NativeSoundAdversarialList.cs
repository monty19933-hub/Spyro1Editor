using System.Collections;

// Finite hostile input. The explicit tripwire fails immediately if a producer
// attempts max+2 reads; this test never uses an actually infinite enumerator.
internal sealed class NativeSoundAdversarialList<T>(T item, int declaredCount, int actualCount, int failAfter) : IReadOnlyList<T>
{
    public int Count => declaredCount;
    public int ItemsRead { get; private set; }
    public T this[int index] => item;
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < actualCount; i++)
        {
            if (i >= failAfter) throw new InvalidOperationException("RED: request enumeration reached max+2.");
            ItemsRead++;
            yield return item;
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
