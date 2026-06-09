namespace PhotoRetouch;

public sealed class BalancedMaskQualityValidator
{
    public BalancedMaskQualityReport Validate(FaceSnapshotMaskSet balancedSnapshot, ShapeBalanceMap map)
    {
        ArgumentNullException.ThrowIfNull(balancedSnapshot);
        ArgumentNullException.ThrowIfNull(map);

        List<string> warnings = new();
        MaskQualityReport quality = balancedSnapshot.QualityReport;
        double safetyScore = quality.Score;
        double warpAlignmentScore = 1.0;
        if (Math.Abs(map.RotationRadians) > Math.PI / 36d)
        {
            safetyScore -= 0.08;
            warpAlignmentScore -= 0.05;
            warnings.Add("shape_rotation_near_limit");
        }

        if (map.LocalWarpRegions.Count > 6)
        {
            safetyScore -= 0.04;
            warpAlignmentScore -= 0.04;
            warnings.Add("many_local_shape_regions");
        }

        double hardProtectAverage = balancedSnapshot.Masks.HardProtectMask.Average();
        double retouchAllowAverage = balancedSnapshot.Masks.RetouchAllowMask.Average();
        double softProtectAverage = balancedSnapshot.Masks.SoftProtectMask.Average();
        double nostrilAverage = balancedSnapshot.Masks.NostrilMask.Average();

        if (hardProtectAverage < 0.0005)
        {
            safetyScore -= 0.10;
            warpAlignmentScore -= 0.12;
            warnings.Add("balanced_hardprotect_mask_too_small");
        }

        if (retouchAllowAverage < 0.001)
        {
            safetyScore -= 0.10;
            warpAlignmentScore -= 0.10;
            warnings.Add("balanced_retouch_allow_mask_too_small");
        }

        if (softProtectAverage > 0 && EstimateOverlap(balancedSnapshot.Masks.SoftProtectMask, balancedSnapshot.Masks.HardProtectMask) > 0.08)
        {
            safetyScore -= 0.05;
            warpAlignmentScore -= 0.08;
            warnings.Add("balanced_softprotect_overlaps_hardprotect");
        }

        if (retouchAllowAverage > 0 && EstimateOverlap(balancedSnapshot.Masks.RetouchAllowMask, balancedSnapshot.Masks.HardProtectMask) > 0.04)
        {
            safetyScore -= 0.07;
            warpAlignmentScore -= 0.12;
            warnings.Add("balanced_retouch_allow_overlaps_hardprotect");
        }

        if (nostrilAverage < 0.00002)
        {
            warpAlignmentScore -= 0.04;
            warnings.Add("balanced_nostril_observation_weak");
        }

        safetyScore = Math.Clamp(safetyScore, 0, 1);
        warpAlignmentScore = Math.Clamp(warpAlignmentScore, 0, 1);
        int maxAllowedShapeStage = safetyScore switch
        {
            >= 0.82 => 10,
            >= 0.72 => 8,
            >= 0.62 => 6,
            >= 0.52 => 4,
            _ => 2
        };

        return new BalancedMaskQualityReport(
            quality.Score,
            safetyScore,
            warpAlignmentScore,
            maxAllowedShapeStage,
            warnings);
    }

    private static double EstimateOverlap(MaskPlane left, MaskPlane right)
    {
        MaskPlane.EnsureSameSize(left, right);
        double overlap = 0;
        double leftSum = 0;
        for (int index = 0; index < left.Values.Length; index++)
        {
            overlap += Math.Min(left.Values[index], right.Values[index]);
            leftSum += left.Values[index];
        }

        return leftSum <= 0.00001 ? 0 : overlap / leftSum;
    }
}
