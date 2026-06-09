using CbmEngine.Abstractions;
using ViceSharp.Abstractions;

namespace CbmEngine.Systems.LineDirector;

public sealed class LineDirector
{
    private readonly ICommodoreMachine _machine;
    private readonly LineProgram _program;
    private readonly int _cyclesPerLine;
    private readonly int _totalLines;
    private int _currentLine;

    public int CurrentLine => _currentLine;
    public int CyclesPerLine => _cyclesPerLine;
    public int TotalLines => _totalLines;

    public LineDirector(ICommodoreMachine machine, LineProgram program)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _program = program ?? throw new ArgumentNullException(nameof(program));
        _cyclesPerLine = machine.Capabilities.CyclesPerLine;
        _totalLines = machine.Capabilities.RasterLines;
    }

    public void StepLine()
    {
        if (_program.TryGet(_currentLine, out var writes))
            for (int i = 0; i < writes.Count; i++)
            {
                var w = writes[i];
                _machine.Bus.Write(w.Address, w.Value);
            }

        _machine.Clock.Step(_cyclesPerLine);
        _currentLine = (_currentLine + 1) % _totalLines;
    }

    public void RunFrame()
    {
        int start = _currentLine;
        for (int i = 0; i < _totalLines; i++) StepLine();
        if (_currentLine != start)
            _currentLine = start;
    }

    public IReadOnlyList<int> RunFrameAndRecordRasterLines()
    {
        var observed = new List<int>(_totalLines);
        for (int i = 0; i < _totalLines; i++)
        {
            observed.Add(_machine.VideoChip.CurrentRasterLine);
            StepLine();
        }
        return observed;
    }
}
