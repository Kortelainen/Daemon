namespace Daemon.Services;

/// <summary>
/// Fixed-size circular buffer for sparkline history.
/// Always returns values oldest-first for left-to-right rendering.
/// </summary>
public sealed class RollingBuffer(int capacity)
{
    private readonly double[] _data = new double[capacity];
    private int _index;
    private bool _filled;

    public int Capacity => capacity;

    public void Push(double value)
    {
        _data[_index] = value;
        _index = (_index + 1) % capacity;
        if (_index == 0) _filled = true;
    }

    public double[] ToArray()
    {
        if (!_filled)
            return _data[.._index];

        var result = new double[capacity];
        int start  = _index;
        for (int i = 0; i < capacity; i++)
            result[i] = _data[(start + i) % capacity];
        return result;
    }

    public double Max() => _data.Max();
}
