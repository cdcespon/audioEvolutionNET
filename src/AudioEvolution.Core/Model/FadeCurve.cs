namespace AudioEvolution.Core.Model;

public enum FadeCurve
{
    Linear,
    Logarithmic,
    Exponential,
    SCurve,
    EqualPower
}

public static class FadeCurveEvaluator
{
    /// <summary>Evaluates gain (0..1) at normalized position t (0..1) along a fade-in curve.</summary>
    public static float EvaluateFadeIn(FadeCurve curve, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return curve switch
        {
            FadeCurve.Linear => (float)t,
            FadeCurve.Logarithmic => (float)Math.Sqrt(t),
            FadeCurve.Exponential => (float)(t * t),
            FadeCurve.SCurve => (float)(t * t * (3 - 2 * t)),
            FadeCurve.EqualPower => (float)Math.Sin(t * Math.PI / 2),
            _ => (float)t
        };
    }

    /// <summary>Evaluates gain (0..1) at normalized position t (0..1) along a fade-out curve.</summary>
    public static float EvaluateFadeOut(FadeCurve curve, double t) => EvaluateFadeIn(curve, 1.0 - t);
}
