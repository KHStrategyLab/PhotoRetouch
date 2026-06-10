namespace PhotoRetouch.AnchorMesh;

public sealed class KAnchorMeasureEngine
{
    public AnchorFaceMeasurements Measure(YuNetAnchorSet anchors, MaskPlane? faceMask = null)
    {
        AnchorFaceMeasurements measurements = new()
        {
            EyeDistance = anchors.EyeDistance,
            FaceBoxWidth = anchors.FaceBox.Width,
            FaceBoxHeight = anchors.FaceBox.Height,
            RotationRad = anchors.FaceAngleRad,
            RotationDeg = anchors.FaceAngleRad * 180.0f / MathF.PI,
            EyeToNoseDistance = Distance(anchors.EyeCenter.X, anchors.EyeCenter.Y, anchors.NoseTip.X, anchors.NoseTip.Y),
            NoseToMouthDistance = Distance(anchors.NoseTip.X, anchors.NoseTip.Y, anchors.MouthCenter.X, anchors.MouthCenter.Y)
        };

        measurements.FaceWidthToEyeDistanceRatio = SafeRatio(measurements.FaceBoxWidth, measurements.EyeDistance);
        measurements.FaceHeightToEyeDistanceRatio = SafeRatio(measurements.FaceBoxHeight, measurements.EyeDistance);

        if (faceMask is not null)
        {
            AddMaskMeasurements(measurements, anchors, faceMask);
        }
        else
        {
            AddAnchorFallbackMeasurements(measurements, anchors, "face_mask_missing_measurements_use_anchor_fallback");
        }

        float estimatedChinY = measurements.FaceMaskBottomY > 0
            ? measurements.FaceMaskBottomY
            : anchors.FaceBox.Bottom;
        measurements.MouthToChinDistance = MathF.Max(0, estimatedChinY - anchors.MouthCenter.Y);
        measurements.EyeLineToMouthDistance = MathF.Max(0, anchors.MouthCenter.Y - anchors.EyeCenter.Y);
        measurements.EyeLineToChinDistance = MathF.Max(0, estimatedChinY - anchors.EyeCenter.Y);
        measurements.PhiltrumLength = MathF.Max(0, anchors.MouthCenter.Y - anchors.NoseTip.Y);
        measurements.ChinLength = MathF.Max(0, estimatedChinY - anchors.MouthCenter.Y);
        measurements.EyeLineToChinToEyeDistanceRatio = SafeRatio(measurements.EyeLineToChinDistance, measurements.EyeDistance);
        measurements.HorizontalFaceOutlineWidth = measurements.FaceMaskWidth > 0 ? measurements.FaceMaskWidth : measurements.FaceBoxWidth;
        System.Drawing.RectangleF anchorFallbackBox = CreateAnchorFallbackBox(anchors);
        measurements.HorizontalCheekWidth = MeasureHorizontalMaskWidthAtY(faceMask, anchorFallbackBox, anchors.EyeCenter.Y + measurements.EyeDistance * 0.48f);
        measurements.HorizontalJawWidth = MeasureHorizontalMaskWidthAtY(faceMask, anchorFallbackBox, anchors.MouthCenter.Y + measurements.EyeDistance * 0.58f);
        measurements.VerticalForeheadToChinDistance = MathF.Max(0, estimatedChinY - measurements.FaceMaskTopY);
        measurements.CorrectedCenterX = (anchors.EyeCenter.X * 0.45f) + (anchors.NoseTip.X * 0.35f) + (anchors.MouthCenter.X * 0.20f);
        measurements.CorrectedCenterY = (anchors.EyeCenter.Y * 0.35f) + (anchors.NoseTip.Y * 0.35f) + (anchors.MouthCenter.Y * 0.30f);
        measurements.CenterOffsetX = measurements.CorrectedCenterX - (measurements.FaceMaskLeftX + measurements.FaceMaskRightX) * 0.5f;
        measurements.CenterOffsetY = measurements.CorrectedCenterY - (measurements.FaceMaskTopY + measurements.FaceMaskBottomY) * 0.5f;

        AddFeatureRatioMeasurements(measurements, anchors, faceMask, estimatedChinY);
        Validate(measurements);
        return measurements;
    }

    private static void AddFeatureRatioMeasurements(AnchorFaceMeasurements measurements, YuNetAnchorSet anchors, MaskPlane? faceMask, float estimatedChinY)
    {
        float faceWidth = measurements.HorizontalFaceOutlineWidth > 1
            ? measurements.HorizontalFaceOutlineWidth
            : measurements.FaceMaskWidth > 1
                ? measurements.FaceMaskWidth
                : anchors.FaceBox.Width;
        float faceTopY = measurements.FaceMaskTopY > 0 ? measurements.FaceMaskTopY : anchors.FaceBox.Top;
        float faceHeight = estimatedChinY - faceTopY;
        if (faceWidth <= 1 || faceHeight <= 1)
        {
            measurements.Warnings.Add("feature_ratio_measurements_face_size_invalid");
            return;
        }

        float faceCenterX = measurements.CorrectedCenterX;
        float mouthWidth = Distance(
            anchors.LeftMouthCorner.X,
            anchors.LeftMouthCorner.Y,
            anchors.RightMouthCorner.X,
            anchors.RightMouthCorner.Y);
        float noseRootY = anchors.EyeCenter.Y + anchors.EyeDistance * 0.16f;
        float noseBaseY = anchors.NoseTip.Y + measurements.NoseToMouthDistance * 0.22f;
        float estimatedNoseLength = MathF.Max(0, noseBaseY - noseRootY);
        float lowerFaceHeight = MathF.Max(1, estimatedChinY - noseBaseY);

        measurements.FaceHeightToWidthRatio = SafeRatio(faceHeight, faceWidth);
        measurements.EyeCenterYToFaceHeightRatio = SafeRatio(anchors.EyeCenter.Y - faceTopY, faceHeight);
        measurements.EyeDistanceToFaceWidthRatio = SafeRatio(anchors.EyeDistance, faceWidth);
        measurements.EyeHeightBalanceScore = SafeRatio(MathF.Abs(anchors.LeftEye.Y - anchors.RightEye.Y), faceHeight);
        measurements.EyeLevelScore = SafeRatio(MathF.Abs(anchors.LeftEye.Y - anchors.RightEye.Y), faceHeight);
        measurements.EyeCenterOffsetToFaceWidth = SafeRatio(MathF.Abs(anchors.EyeCenter.X - faceCenterX), faceWidth);
        measurements.EyeLineAngleDeg = anchors.FaceAngleRad * 180.0f / MathF.PI;
        measurements.NoseTipYToFaceHeightRatio = SafeRatio(anchors.NoseTip.Y - faceTopY, faceHeight);
        measurements.NoseBaseYToFaceHeightRatio = SafeRatio(noseBaseY - faceTopY, faceHeight);
        measurements.NoseCenterOffsetToFaceWidth = SafeRatio(MathF.Abs(anchors.NoseTip.X - faceCenterX), faceWidth);
        measurements.NoseEyeCenterOffsetToFaceWidth = SafeRatio(MathF.Abs(anchors.NoseTip.X - anchors.EyeCenter.X), faceWidth);
        measurements.EstimatedNoseLengthToFaceHeightRatio = SafeRatio(estimatedNoseLength, faceHeight);
        measurements.EstimatedNoseLengthToEyeDistanceRatio = SafeRatio(estimatedNoseLength, anchors.EyeDistance);
        measurements.MouthCenterYToFaceHeightRatio = SafeRatio(anchors.MouthCenter.Y - faceTopY, faceHeight);
        measurements.MouthCenterOffsetToFaceWidth = SafeRatio(MathF.Abs(anchors.MouthCenter.X - faceCenterX), faceWidth);
        measurements.MouthNoseCenterOffsetToFaceWidth = SafeRatio(MathF.Abs(anchors.MouthCenter.X - anchors.NoseTip.X), faceWidth);
        measurements.MouthEyeCenterOffsetToFaceWidth = SafeRatio(MathF.Abs(anchors.MouthCenter.X - anchors.EyeCenter.X), faceWidth);
        measurements.MouthWidthToEyeDistanceRatio = SafeRatio(mouthWidth, anchors.EyeDistance);
        measurements.MouthWidthToFaceWidthRatio = SafeRatio(mouthWidth, faceWidth);
        measurements.MouthCornerLevelScore = SafeRatio(MathF.Abs(anchors.LeftMouthCorner.Y - anchors.RightMouthCorner.Y), faceHeight);
        measurements.MouthCornerSlopeDeg = MathF.Atan2(
            anchors.RightMouthCorner.Y - anchors.LeftMouthCorner.Y,
            anchors.RightMouthCorner.X - anchors.LeftMouthCorner.X) * 180.0f / MathF.PI;
        measurements.MouthWidthBalanceScore = SafeRatio(
            MathF.Abs((anchors.MouthCenter.X - anchors.LeftMouthCorner.X) - (anchors.RightMouthCorner.X - anchors.MouthCenter.X)),
            mouthWidth);
        measurements.PhiltrumToLowerFaceRatio = SafeRatio(measurements.PhiltrumLength, lowerFaceHeight);
        measurements.LowerFacePhiltrumLipChinGuideRatio = SafeRatio(measurements.ChinLength, MathF.Max(1, measurements.PhiltrumLength));

        System.Drawing.RectangleF anchorFallbackBox = CreateAnchorFallbackBox(anchors);
        MaskSpan widthAt20 = MeasureHorizontalMaskSpanAtY(faceMask, anchorFallbackBox, faceTopY + faceHeight * 0.20f);
        MaskSpan widthAt35 = MeasureHorizontalMaskSpanAtY(faceMask, anchorFallbackBox, faceTopY + faceHeight * 0.35f);
        MaskSpan widthAt50 = MeasureHorizontalMaskSpanAtY(faceMask, anchorFallbackBox, faceTopY + faceHeight * 0.50f);
        MaskSpan widthAt65 = MeasureHorizontalMaskSpanAtY(faceMask, anchorFallbackBox, faceTopY + faceHeight * 0.65f);
        MaskSpan widthAt80 = MeasureHorizontalMaskSpanAtY(faceMask, anchorFallbackBox, faceTopY + faceHeight * 0.80f);
        MaskSpan widthAt90 = MeasureHorizontalMaskSpanAtY(faceMask, anchorFallbackBox, faceTopY + faceHeight * 0.90f);

        measurements.WidthAt20 = widthAt20.Width;
        measurements.WidthAt35 = widthAt35.Width;
        measurements.WidthAt50 = widthAt50.Width;
        measurements.WidthAt65 = widthAt65.Width;
        measurements.WidthAt80 = widthAt80.Width;
        measurements.WidthAt90 = widthAt90.Width;
        measurements.ForeheadWidthRatio = SafeRatio(widthAt20.Width, faceWidth);
        measurements.EyeLevelWidthRatio = SafeRatio(widthAt35.Width, faceWidth);
        measurements.CheekLevelWidthRatio = SafeRatio(widthAt50.Width, faceWidth);
        measurements.MouthLevelWidthRatio = SafeRatio(widthAt65.Width, faceWidth);
        measurements.JawLevelWidthRatio = SafeRatio(widthAt80.Width, faceWidth);
        measurements.ChinLevelWidthRatio = SafeRatio(widthAt90.Width, faceWidth);

        measurements.CheekFaceWidthRatio = SafeRatio(measurements.HorizontalCheekWidth, faceWidth);
        measurements.JawFaceWidthRatio = SafeRatio(measurements.HorizontalJawWidth, faceWidth);
        measurements.ChinFaceWidthRatio = SafeRatio(widthAt90.Width, faceWidth);
        measurements.JawWidthToCheekWidthRatio = SafeRatio(measurements.HorizontalJawWidth, measurements.HorizontalCheekWidth);
        measurements.JawMidToCheekWidthRatio = SafeRatio(widthAt80.Width, widthAt50.Width);
        measurements.ChinWidthToCheekWidthRatio = SafeRatio(widthAt90.Width, widthAt50.Width);
        measurements.JawToChinTaperRatio = SafeRatio(widthAt90.Width, measurements.HorizontalJawWidth);
        measurements.LowerFaceToFaceHeightRatio = SafeRatio(lowerFaceHeight, faceHeight);
        measurements.MouthToChinToLowerFaceRatio = SafeRatio(measurements.MouthToChinDistance, lowerFaceHeight);
        measurements.ContourBalanceScore = AverageContourBalance(faceCenterX, faceWidth, widthAt35, widthAt50, widthAt65, widthAt80, widthAt90);
        measurements.LowerContourBalanceScore = AverageContourBalance(faceCenterX, faceWidth, widthAt65, widthAt80, widthAt90);
        measurements.FeatureRatioGuideConfidence = Math.Clamp(
            measurements.MaskCoverageConfidence * 0.55f + anchors.Score * 0.35f + 0.10f,
            0.0f,
            1.0f);
        measurements.EyeMetricGuideConfidence = Math.Clamp(
            measurements.MaskCoverageConfidence * 0.45f + anchors.Score * 0.40f + (MathF.Abs(measurements.EyeLineAngleDeg) < 8 ? 0.15f : 0.05f),
            0.0f,
            1.0f);
        measurements.MouthMetricGuideConfidence = Math.Clamp(
            measurements.MaskCoverageConfidence * 0.35f + anchors.Score * 0.40f + (measurements.MouthWidthToEyeDistanceRatio is > 0.55f and < 1.25f ? 0.25f : 0.08f),
            0.0f,
            1.0f);
        measurements.ContourMetricGuideConfidence = Math.Clamp(
            measurements.MaskCoverageConfidence * 0.70f + anchors.Score * 0.20f + 0.10f,
            0.0f,
            1.0f);
        measurements.Warnings.Add("eye_width_height_pupil_iris_metrics_require_dense_landmarks");
        measurements.Warnings.Add("eyelash_root_tip_mascara_shadow_metrics_require_eye_detail_masks");
        measurements.Warnings.Add("dark_circle_under_eye_shadow_metrics_require_under_eye_component_masks");
        measurements.Warnings.Add("brow_position_width_thickness_arch_density_metrics_require_dense_landmarks");
        measurements.Warnings.Add("lip_height_inner_mouth_teeth_cupid_bow_metrics_require_dense_landmarks");
        measurements.Warnings.Add("lip_surface_correction_requires_confident_lip_surface_mask");
        measurements.Warnings.Add("philtrum_anchor_distance_ratio_metrics_require_nose_base_upper_lip_and_mustache_masks");
        measurements.Warnings.Add("face_vertical_distance_angle_metrics_require_dense_lip_brow_chin_anchors_and_roll_correction");
        measurements.Warnings.Add("face_component_position_ratio_guides_require_landmark_snapped_masks_and_overlap_validation");
        measurements.Warnings.Add("nose_width_wing_nostril_bridge_metrics_require_dense_landmarks");
        measurements.Warnings.Add("chin_width_neck_width_contour_noise_metrics_require_dense_contours");
        measurements.Warnings.Add("face_shape_analysis_requires_dense_contours_occlusion_masks_and_user_controlled_tools");
        measurements.Warnings.Add("hair_boundary_strand_hairline_flyaway_metrics_require_hair_segmentation_and_matting");
        measurements.Warnings.Add("flyaway_hair_removal_requires_user_control_strand_masks_and_restoration_confidence");
        measurements.Warnings.Add("ear_visibility_occlusion_color_shadow_metrics_require_roi_segmentation_and_virtual_points");
        measurements.Warnings.Add("forehead_hairline_wrinkle_shine_shadow_metrics_require_forehead_segmentation_and_brow_hair_masks");
        measurements.Warnings.Add("wrinkle_fold_age_line_metrics_require_region_wrinkle_masks_and_line_classification");
        measurements.Warnings.Add("spot_acne_blemish_pigmentation_metrics_require_skin_mark_classification_and_identity_protection");
        measurements.Warnings.Add("manual_skin_smoothing_requires_slider_trigger_protection_masks_and_texture_guard");
        measurements.Warnings.Add("skin_smoothing_scale_threshold_uniformity_requires_detail_layers_and_texture_analysis");
        measurements.Warnings.Add("beard_mustache_long_facial_hair_landmarks_require_beard_masks_and_virtual_points");
        measurements.Warnings.Add("beard_stubble_shaving_shadow_requires_user_control_and_beard_aware_masks");
        measurements.Warnings.Add("skin_texture_pore_plastic_skin_metrics_require_texture_masks_and_before_after_detail_analysis");
        measurements.Warnings.Add("skin_color_body_tone_matching_requires_user_control_clean_reference_and_lighting_maps");
        measurements.Warnings.Add("cheekbone_zygoma_highlight_shadow_metrics_require_dense_contours_and_cheek_masks");
        measurements.Warnings.Add("square_jaw_masseter_mandibular_angle_metrics_require_dense_jaw_contours_and_occlusion_masks");
        measurements.Warnings.Add("under_jaw_shadow_softening_metrics_require_jaw_neck_shadow_and_occlusion_masks");
    }

    private static void AddMaskMeasurements(AnchorFaceMeasurements measurements, YuNetAnchorSet anchors, MaskPlane faceMask)
    {
        const double threshold = 0.35;
        int minX = faceMask.Width;
        int minY = faceMask.Height;
        int maxX = -1;
        int maxY = -1;
        int coveredPixels = 0;
        System.Drawing.RectangleF anchorSearchBox = Inflate(CreateAnchorFallbackBox(anchors), 0.12f);
        int anchorSearchPixels = Math.Max(1, (int)Math.Round(anchorSearchBox.Width * anchorSearchBox.Height));

        int faceLeft = Math.Clamp((int)MathF.Floor(anchorSearchBox.Left), 0, faceMask.Width - 1);
        int faceTop = Math.Clamp((int)MathF.Floor(anchorSearchBox.Top), 0, faceMask.Height - 1);
        int faceRight = Math.Clamp((int)MathF.Ceiling(anchorSearchBox.Right), 0, faceMask.Width - 1);
        int faceBottom = Math.Clamp((int)MathF.Ceiling(anchorSearchBox.Bottom), 0, faceMask.Height - 1);

        for (int y = faceTop; y <= faceBottom; y++)
        {
            for (int x = faceLeft; x <= faceRight; x++)
            {
                if (faceMask[x, y] < threshold)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                coveredPixels++;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            AddAnchorFallbackMeasurements(measurements, anchors, "face_mask_empty_use_anchor_fallback");
            return;
        }

        measurements.FaceMaskLeftX = minX;
        measurements.FaceMaskRightX = maxX;
        measurements.FaceMaskTopY = minY;
        measurements.FaceMaskBottomY = maxY;
        measurements.FaceMaskWidth = maxX - minX + 1;
        measurements.FaceMaskHeight = maxY - minY + 1;
        measurements.MaskHeightToEyeDistanceRatio = SafeRatio(measurements.FaceMaskHeight, measurements.EyeDistance);
        measurements.MaskCoverageConfidence = Math.Clamp((float)(coveredPixels / (double)anchorSearchPixels), 0.0f, 1.0f);
    }

    private static void AddAnchorFallbackMeasurements(AnchorFaceMeasurements measurements, YuNetAnchorSet anchors, string warning)
    {
        if (anchors.EyeDistance <= 1)
        {
            measurements.FaceMaskLeftX = anchors.FaceBox.Left;
            measurements.FaceMaskRightX = anchors.FaceBox.Right;
            measurements.FaceMaskTopY = anchors.FaceBox.Top;
            measurements.FaceMaskBottomY = anchors.FaceBox.Bottom;
            measurements.FaceMaskWidth = anchors.FaceBox.Width;
            measurements.FaceMaskHeight = anchors.FaceBox.Height;
            measurements.MaskHeightToEyeDistanceRatio = measurements.FaceHeightToEyeDistanceRatio;
            measurements.MaskCoverageConfidence = 0.05f;
            measurements.Warnings.Add("anchor_fallback_failed_use_facebox_debug_only");
            return;
        }

        System.Drawing.RectangleF anchorBox = CreateAnchorFallbackBox(anchors);
        measurements.FaceMaskLeftX = anchorBox.Left;
        measurements.FaceMaskRightX = anchorBox.Right;
        measurements.FaceMaskTopY = anchorBox.Top;
        measurements.FaceMaskBottomY = anchorBox.Bottom;
        measurements.FaceMaskWidth = anchorBox.Width;
        measurements.FaceMaskHeight = anchorBox.Height;
        measurements.MaskHeightToEyeDistanceRatio = SafeRatio(measurements.FaceMaskHeight, measurements.EyeDistance);
        measurements.MaskCoverageConfidence = 0.28f;
        measurements.Warnings.Add(warning);
    }

    private static System.Drawing.RectangleF CreateAnchorFallbackBox(YuNetAnchorSet anchors)
    {
        float eyeDistance = MathF.Max(1, anchors.EyeDistance);
        float noseToMouth = MathF.Max(eyeDistance * 0.30f, anchors.MouthCenter.Y - anchors.NoseTip.Y);
        float mouthToChin = Math.Clamp(MathF.Max(eyeDistance * 0.72f, noseToMouth * 1.08f), eyeDistance * 0.55f, eyeDistance * 1.35f);
        float chinX = anchors.EyeCenter.X * 0.18f + anchors.NoseTip.X * 0.24f + anchors.MouthCenter.X * 0.58f;
        float chinY = anchors.MouthCenter.Y + mouthToChin;
        float centerX = anchors.EyeCenter.X * 0.30f + anchors.NoseTip.X * 0.30f + anchors.MouthCenter.X * 0.28f + chinX * 0.12f;
        float topY = anchors.EyeCenter.Y - eyeDistance * 1.22f;
        float bottomY = MathF.Max(chinY, anchors.MouthCenter.Y + eyeDistance * 1.05f);
        float width = eyeDistance * 2.72f;
        float height = MathF.Max(eyeDistance * 3.05f, bottomY - topY);
        float centerY = (topY + bottomY) * 0.5f;
        return new System.Drawing.RectangleF(
            centerX - width * 0.5f,
            centerY - height * 0.5f,
            width,
            height);
    }

    private static System.Drawing.RectangleF Inflate(System.Drawing.RectangleF rectangle, float ratio)
    {
        float dx = rectangle.Width * ratio;
        float dy = rectangle.Height * ratio;
        return new System.Drawing.RectangleF(
            rectangle.Left - dx,
            rectangle.Top - dy,
            rectangle.Width + dx * 2,
            rectangle.Height + dy * 2);
    }

    private static void Validate(AnchorFaceMeasurements measurements)
    {
        if (measurements.EyeDistance < 20)
        {
            measurements.Warnings.Add("eye_distance_too_small");
        }

        if (measurements.FaceMaskHeight <= 0 || measurements.FaceMaskWidth <= 0)
        {
            measurements.Warnings.Add("face_mask_size_invalid");
        }

        if (measurements.MaskHeightToEyeDistanceRatio is < 2.2f or > 6.2f)
        {
            measurements.Warnings.Add("face_mask_height_eye_distance_ratio_outside_expected_range");
        }

        if (measurements.FaceHeightToWidthRatio is > 0 and (< 1.18f or > 1.72f))
        {
            measurements.Warnings.Add("face_height_width_ratio_outside_loose_portrait_range");
        }

        if (measurements.EyeDistanceToFaceWidthRatio is > 0 and (< 0.30f or > 0.54f))
        {
            measurements.Warnings.Add("eye_distance_face_width_ratio_outside_loose_portrait_range");
        }

        if (measurements.EyeLevelScore > 0.030f)
        {
            measurements.Warnings.Add("eye_level_score_suggests_roll_pose_or_detection_issue");
        }

        if (measurements.MouthWidthToEyeDistanceRatio is > 0 and (< 0.62f or > 1.18f))
        {
            measurements.Warnings.Add("mouth_width_eye_distance_ratio_outside_loose_portrait_range");
        }

        if (measurements.MouthCornerLevelScore > 0.030f)
        {
            measurements.Warnings.Add("mouth_corner_level_score_suggests_expression_pose_or_detection_issue");
        }

        if (measurements.EstimatedNoseLengthToFaceHeightRatio is > 0 and (< 0.20f or > 0.36f))
        {
            measurements.Warnings.Add("estimated_nose_length_face_height_ratio_outside_loose_portrait_range");
        }

        if (measurements.JawWidthToCheekWidthRatio is > 0 and (< 0.54f or > 1.02f))
        {
            measurements.Warnings.Add("jaw_width_cheek_width_ratio_outside_loose_portrait_range");
        }

        if (measurements.LowerFaceToFaceHeightRatio is > 0 and (< 0.24f or > 0.44f))
        {
            measurements.Warnings.Add("lower_face_height_ratio_outside_loose_portrait_range");
        }

        if (measurements.ChinWidthToCheekWidthRatio is > 0 and (< 0.12f or > 0.46f))
        {
            measurements.Warnings.Add("chin_width_cheek_width_ratio_outside_loose_portrait_range");
        }

        if (measurements.ContourBalanceScore > 0.060f)
        {
            measurements.Warnings.Add("contour_balance_score_suggests_pose_hair_shadow_or_detection_issue");
        }

        if (measurements.LowerContourBalanceScore > 0.060f)
        {
            measurements.Warnings.Add("lower_contour_balance_score_suggests_pose_hair_shadow_or_detection_issue");
        }
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        float dx = bx - ax;
        float dy = by - ay;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float MeasureHorizontalMaskWidthAtY(MaskPlane? mask, System.Drawing.RectangleF fallbackFaceBox, float y)
    {
        return MeasureHorizontalMaskSpanAtY(mask, fallbackFaceBox, y).Width;
    }

    private static MaskSpan MeasureHorizontalMaskSpanAtY(MaskPlane? mask, System.Drawing.RectangleF fallbackFaceBox, float y)
    {
        if (mask is null)
        {
            return new MaskSpan(fallbackFaceBox.Left, fallbackFaceBox.Right, fallbackFaceBox.Width, false);
        }

        int yy = Math.Clamp((int)MathF.Round(y), 0, mask.Height - 1);
        int minX = mask.Width;
        int maxX = -1;
        int left = Math.Clamp((int)MathF.Floor(fallbackFaceBox.Left), 0, mask.Width - 1);
        int right = Math.Clamp((int)MathF.Ceiling(fallbackFaceBox.Right), 0, mask.Width - 1);
        for (int x = left; x <= right; x++)
        {
            if (mask[x, yy] < 0.35)
            {
                continue;
            }

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
        }

        return maxX < minX
            ? new MaskSpan(fallbackFaceBox.Left, fallbackFaceBox.Right, fallbackFaceBox.Width, false)
            : new MaskSpan(minX, maxX, maxX - minX + 1, true);
    }

    private static float AverageContourBalance(float faceCenterX, float faceWidth, params MaskSpan[] spans)
    {
        if (faceWidth <= 0 || spans.Length == 0)
        {
            return 0;
        }

        float sum = 0;
        int count = 0;
        foreach (MaskSpan span in spans)
        {
            if (!span.HasMask || span.Width <= 0)
            {
                continue;
            }

            float leftDistance = MathF.Max(0, faceCenterX - span.Left);
            float rightDistance = MathF.Max(0, span.Right - faceCenterX);
            sum += MathF.Abs(leftDistance - rightDistance) / faceWidth;
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private static float SafeRatio(float value, float divisor)
    {
        return divisor <= 0.001f ? 0 : value / divisor;
    }

    private readonly record struct MaskSpan(float Left, float Right, float Width, bool HasMask);
}
