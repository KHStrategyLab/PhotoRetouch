namespace PhotoRetouch;

public interface IStandardMaskWarper
{
    WarpedMaskSet Warp(StandardMaskSet standardMasks, MaskWarpInput input);
}
