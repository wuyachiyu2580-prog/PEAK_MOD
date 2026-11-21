from pathlib import Path

# Reuse the previous read-only audit while preserving its original evidence.
source = Path(__file__).resolve().parents[1] / 'whysolaggy-run-20261115' / 'audit.py'
exec(compile(source.read_text(encoding='utf-8'), str(source), 'exec'),
     {'__file__': __file__, '__name__': '__main__'})
