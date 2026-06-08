이 파일은 UTF-8 기준으로 작성됨

# K Retouch Pro - Order Backup

목적:

* 현재까지 전달된 작업 오더의 순서, 목적, 상태를 안전하게 백업한다.
* 이 문서는 기존 오더를 변경하거나 재해석하지 않는다.
* 새 기능 구현, 리팩토링, UI 변경, 필터 변경은 이 백업 작업에 포함하지 않는다.

상태값:

* Planned
* InProgress
* Done
* Hold
* NeedsReview

## 1. Snapshot Mask 설계

목적:

* 사진 한 장마다 얼굴 전용 Snapshot Mask를 만들고, HardProtect / SoftProtect / RetouchAllow 구조를 정의한다.

상태:

* Done

관련 모듈:

* `docs/ENGINE_DESIGN.md`
* `docs/FEATURE_STATUS_AND_ROADMAP.md`
* `AGENTS.md`
* `Tools/Masking/FaceSnapshotMaskSet.cs`
* `Tools/Masking/FaceMaskSet.cs`

구현 여부:

* 현재 코드와 문서에 반영됨.

주의:

* Stage 변경만으로 Snapshot Mask를 다시 생성하지 않는다.

다음 연결:

* Dummy Snapshot Mask 검증

## 2. Dummy Snapshot Mask 검증

목적:

* AI 모델 없이 더미 마스크로 Snapshot 생성, Debug Overlay, Cache Reuse 구조가 동작하는지 검증한다.

상태:

* Done

관련 모듈:

* `Tools/Masking/DummySnapshotMaskEngine.cs`
* `Tools/Masking/DummyRetouchFilter.cs`
* `MainWindow.xaml`
* `MainWindow.xaml.cs`

구현 여부:

* 현재 코드에 반영됨.
* 이후 Standard Mask Warp 단계로 연결됨.

주의:

* 더미 마스크는 실제 품질 판단용이 아니라 구조 검증용이다.

다음 연결:

* Standard Mask Resource + Warp 구조

## 3. Standard Mask Resource + Warp 구조

목적:

* 미리 준비한 표준 마스크를 현재 이미지의 얼굴 위치에 맞춰 scale / rotate / translate 방식으로 입히는 구조를 만든다.

상태:

* Done

관련 모듈:

* `StandardMasks/`
* `Tools/Masking/StandardMaskSet.cs`
* `Tools/Masking/StandardMaskLoader.cs`
* `Tools/Masking/MaskWarpInput.cs`
* `Tools/Masking/IStandardMaskWarper.cs`
* `Tools/Masking/StandardAffineMaskWarper.cs`
* `Tools/Masking/StandardMaskWarpEngine.cs`

구현 여부:

* 현재 코드에 반영됨.

주의:

* Triangle Mesh Warp는 아직 구현하지 않는다.
* 표준 PNG가 없으면 내부 생성 마스크를 사용한다.

다음 연결:

* FaceBox / 얼굴 기준점 자동화

## 4. FaceBox / 얼굴 기준점 자동화

목적:

* 얼굴 위치와 기준점을 자동화하여 MaskWarpInput의 기준 정확도를 높인다.

상태:

* Done

관련 모듈:

* `Tools/Masking/IFaceAnalyzer.cs`
* `Tools/Masking/FaceAnalyzerResult.cs`
* `Tools/Masking/TemporaryFaceAnalyzer.cs`
* `Tools/Masking/MaskWarpInput.cs`
* `Tools/Masking/StandardMaskWarpEngine.cs`
* `Models/PhotoItem.cs`

구현 여부:

* `TemporaryFaceAnalyzer` 기반 자동 FaceBox / 기준점 구조가 코드에 반영됨.
* 실제 FaceDetection / FaceLandmark 모델 연결은 아직 진행하지 않음.

주의:

* 현재 analyzer는 구조 검증용이며 실제 AI 모델이 아니다.

다음 연결:

* FaceDetection / FaceLandmark 연결

## 5. FaceDetection / FaceLandmark 연결

목적:

* 실제 얼굴 박스와 눈, 코, 입, 턱 기준점을 검출하여 Warped Standard Mask의 위치 기준을 고도화한다.

상태:

* NeedsReview

관련 모듈:

* `Tools/Masking/FaceAnalysisResult.cs`
* `Tools/Masking/MaskWarpInput.cs`
* `Tools/Masking/StandardMaskWarpEngine.cs`

구현 여부:

* 실제 AI 모델 연결은 아직 없음.
* 현재는 임시 landmarks를 사용함.

주의:

* 실제 모델 연결 전에는 Snapshot / Debug / Cache 구조를 유지한다.

다음 연결:

* NostrilDetector 초기 구현

## 6. NostrilDetector 초기 구현

목적:

* 콧구멍을 피부가 아닌 보호 디테일로 감지하여 HardProtectMask에 포함한다.

상태:

* Planned

관련 모듈:

* `Tools/Masking/FaceMaskSet.cs`
* `Tools/Masking/StandardMaskWarpEngine.cs`
* `Tools/Masking/NostrilDetectorInput.cs`
* `Tools/Masking/NostrilDetector.cs`
* `Tools/Masking/DebugMaskExporter.cs`

구현 여부:

* 초기 NostrilDetector는 코드에 반영됨.
* NoseLowerROI, dark candidate, connected component, warped standard fallback 결합 구조가 구현됨.
* 실제 사진 기준 시각 검증이 필요함.

주의:

* 콧구멍은 피부 보정 대상이 아니라 HardProtect 대상이다.

다음 연결:

* FaceParsing 1차 연결

## 7. FaceParsing 1차 연결

목적:

* Parsing 결과로 Skin / Eye / Eyebrow / Lip / Mouth / Hair / Beard / Glasses 후보를 분리하고 Warped Standard Mask를 보강한다.

상태:

* InProgress

관련 모듈:

* `Tools/Masking/IFaceParsingDetector.cs`
* `Tools/Masking/FaceParsingInput.cs`
* `Tools/Masking/ParsingMaskSet.cs`
* `Tools/Masking/ParsingLabelMapper.cs`
* `Tools/Masking/TemporaryFaceParsingDetector.cs`
* `Tools/Masking/StandardMaskWarpEngine.cs`
* `Tools/Masking/DebugMaskExporter.cs`

구현 여부:

* 인터페이스와 임시 detector 구조는 코드에 반영됨.
* 실제 AI FaceParsing 모델은 아직 연결하지 않음.

주의:

* `TemporaryFaceParsingDetector`는 실제 AI 모델이 아니라 fallback scaffold다.
* 보호 영역은 넓게, RetouchAllow는 보수적으로 유지한다.

다음 연결:

* MaskQualityValidator + Fail-Safe Stage Gate

## 8. MaskQualityValidator + Fail-Safe Stage Gate

목적:

* 마스크 품질에 따라 강한 Stage를 제한하고, 품질이 낮을 때 보정 강도를 안전하게 낮춘다.

상태:

* InProgress

관련 모듈:

* `Tools/Masking/MaskQualityReport.cs`
* `Tools/Masking/StagePreset.cs`
* `Tools/Masking/RetouchStageProcessor.cs`

구현 여부:

* `MaskQualityValidator`와 상세 `MaskQualityReport`가 코드에 반영됨.
* 부위별 품질 점수, 경고, FatalError, MaxAllowedStage, Fail-Safe opacity 구조가 반영됨.

주의:

* RequestedStage와 AppliedStage는 분리되어야 한다.
* Stage 값은 Snapshot cache key에 들어가지 않는다.

다음 연결:

* Retouch Pipeline 1차 + StagePresetMapper

## 9. Retouch Pipeline 1차 + StagePresetMapper

목적:

* SnapshotMask를 기준으로 RetouchAllow / SoftProtect / HardProtect 합성 규칙을 실제 보정 파이프라인에 연결한다.

상태:

* Done

관련 모듈:

* `Tools/Masking/StagePreset.cs`
* `Tools/Masking/RetouchOptions.cs`
* `Tools/Masking/RetouchProcessReport.cs`
* `Tools/Masking/RetouchStageProcessor.cs`
* `Tools/Masking/RetouchDebugExporter.cs`
* `MainWindow.xaml.cs`

구현 여부:

* 현재 코드에 반영됨.

주의:

* HardProtect는 어떤 Stage에서도 원본 유지가 최우선이다.
* Stage 변경 시 SnapshotMask를 재생성하지 않는다.

다음 연결:

* Retouch Filter 품질 고도화 1차

## 10. Retouch Filter 품질 고도화 1차

목적:

* SkinSmooth를 단순 blur에서 자연스러운 피부 스무딩 구조로 개선하고, TextureRestore로 피부결을 일부 복원한다.

상태:

* NeedsReview

관련 모듈:

* `Tools/Masking/RetouchStageProcessor.cs`
* `Tools/Masking/RetouchDebugExporter.cs`
* `Tools/Masking/StagePreset.cs`
* `Tools/Masking/RetouchProcessReport.cs`

구현 여부:

* 현재 코드에 1차 반영됨.
* 실제 사진 기준 시각 검증이 필요함.

주의:

* 이 단계는 고급 잡티 제거가 아니다.
* 피부결이 사라지거나 HardProtect가 변하면 실패로 본다.

다음 연결:

* BlemishReduce / 잡티 제거 1차

## 11. BlemishReduce / 잡티 제거 1차

목적:

* 잡티 제거를 피부 보정 파이프라인 안에서 1차로 연결한다.

상태:

* Planned

관련 모듈:

* `Tools/Masking/RetouchStageProcessor.cs`
* `Tools/PhotoAdjustment/CSharpPreviewEngine.cs`
* 예정 모듈: `BlemishReduceFilter`

구현 여부:

* 기존 preview engine 쪽에 일부 잡티/여드름/점 관련 처리 흔적은 있음.
* SnapshotMask 기반 Retouch Pipeline 안의 BlemishReduce 1차는 아직 진행하지 않음.

주의:

* 점/검버섯과 일반 잡티는 분리한다.
* 눈, 입술, 콧구멍, 머리카락 경계에 적용되면 안 된다.

다음 연결:

* WrinkleSoftReduce / 주름 완화 1차 + 미간 주름 + 주름 후보별 슬라이더 툴셋

## 12. WrinkleSoftReduce / 주름 완화 1차 + 미간 주름 + 주름 후보별 슬라이더 툴셋

목적:

* 주름을 완전히 제거하지 않고 자연스럽게 완화하며, 미간 주름과 후보별 제어 구조를 준비한다.

상태:

* Planned

관련 모듈:

* 예정 모듈: `WrinkleSoftReduceFilter`
* 예정 UI: 주름 후보별 슬라이더 툴셋
* `Tools/Masking/RetouchStageProcessor.cs`

구현 여부:

* 아직 코드에 반영하지 않음.

주의:

* 얼굴 구조를 만드는 주름과 그림자를 모두 없애면 실패다.
* SoftProtect 정책과 충돌하지 않게 진행한다.

다음 연결:

* ToneEven / 피부톤 균일화 1차

## 13. ToneEven / 피부톤 균일화 1차

목적:

* 피부톤을 과하지 않게 균일화하고, 눈/입술/머리카락 등 보호 영역을 제외한다.

상태:

* InProgress

관련 모듈:

* `Tools/Masking/RetouchStageProcessor.cs`
* `Tools/PhotoAdjustment/CSharpPreviewEngine.cs`
* `Tools/PhotoAdjustment/PreviewAdjustment.cs`

구현 여부:

* 기존 preview engine과 pipeline option에 약한 tone-even 구조가 일부 있음.
* SnapshotMask 기반 ToneEven 1차 고도화는 아직 완료되지 않음.

주의:

* 피부색을 과하게 평탄화하지 않는다.
* 보호 영역에 색 균일화가 들어가면 안 된다.

다음 연결:

* TextureRestore / 피부결 복원 고도화

## 14. TextureRestore / 피부결 복원 고도화

목적:

* 피부 보정 뒤에도 자연스러운 피부결, 모공, 미세 명암이 남도록 복원 구조를 고도화한다.

상태:

* InProgress

관련 모듈:

* `Tools/Masking/RetouchStageProcessor.cs`
* `Tools/Masking/StagePreset.cs`
* `Tools/Masking/RetouchDebugExporter.cs`

구현 여부:

* 현재 1차 DetailLayer / TextureRestore 구조는 코드에 반영됨.
* 고도화 단계는 아직 완료되지 않음.

주의:

* TextureRestore가 너무 강하면 보정 효과가 사라지고, 너무 약하면 플라스틱 피부가 된다.

다음 연결:

* 후속 피부 필터 품질 검증 및 실제 모델/필터 고도화

## Hold 항목

### PhotoRetouch 현재 프로젝트 구조 기준 Toolset / Slider / StagePreset 구조 정렬 오더

목적:

* 현재 프로젝트 구조 기준으로 Toolset, Slider, StagePreset 구조를 정렬한다.

상태:

* Hold

관련 모듈:

* 미정

구현 여부:

* 아직 전달/실행하지 않은 보류 항목으로 관리한다.

주의:

* 현재 작업에 포함하지 않는다.
* 사용자가 다시 지시할 때까지 진행하지 않는다.

다음 연결:

* 사용자 재지시 후 결정

## Backup Summary

정리된 오더 개수:

* 14

Hold 처리된 오더:

* PhotoRetouch 현재 프로젝트 구조 기준 Toolset / Slider / StagePreset 구조 정렬 오더

코드 변경 여부:

* 없음. 이 문서 추가만 수행함.

빌드 실행 여부:

* 없음. 백업 작업 지시에 따라 빌드는 실행하지 않음.

## 2026-06-09 Order Sequence Audit Update

추가 확인:

* `ORDER_00`부터 `ORDER_29`까지의 오더 흐름을 다시 대조함.
* `ORDER_13_TONE_EVEN`은 늦게 도착했지만 `ORDER_14_TEXTURE_RESTORE` 앞에 위치해야 하는 실제 오더로 확인됨.
* `ORDER_17`부터 `ORDER_29`까지는 `Queued / Planned` 상태로 보관함.
* `ORDER_30_HIGH_RES_PERFORMANCE_OPTIMIZATION`은 `ORDER_29`의 후속 오더로 언급되었지만, 전문은 아직 도착하지 않음.

추가 문서:

* `docs/orders/ORDER_SEQUENCE_AUDIT_2026-06-09.md`

현재 주의:

* `ToneEven`은 현재 단순 처리 단계만 연결되어 있고, 전용 `ToneEvenFilter` / Toolset / Slider 정렬은 아직 완료되지 않았다.
* 다음 실제 진행은 `ORDER_16` 빌드 검증 후 `ORDER_17_PROJECT_STRUCTURE_TOOLSET_SLIDER`가 맞다.
