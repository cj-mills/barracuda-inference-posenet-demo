using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using CJM.DeepLearningImageProcessor;
using CJM.HumanPose2DToolkit;
using CJM.BarracudaInference.PoseNet;


public class InferenceController : MonoBehaviour
{
    #region Fields

    [Header("Components")]
    [SerializeField, Tooltip("Responsible for image preprocessing")]
    private ImageProcessor imageProcessor;
    [SerializeField, Tooltip("Executes PoseNet model for pose estimation")]
    private PoseNetPoseEstimator modelRunner;
    [SerializeField, Tooltip("Manages user interface updates")]
    private UIController uiController;
    [SerializeField, Tooltip("Visualizes human pose skeletons")]
    private HumanPose2DVisualizer humanPose2DVisualizer;
    [SerializeField, Tooltip("Renders the input image on a screen")]
    private MeshRenderer screenRenderer;

    [Header("Data Processing")]
    [Tooltip("The target dimensions for the processed image")]
    [SerializeField] private int targetDim = 224;
    [Tooltip("Flag to use compute shaders for processing input images.")]
    [SerializeField] private bool useComputeShaders = false;

    [Header("Output Processing")]
    [SerializeField, Tooltip("Threshold for confidence score filtering, range: [0, 1]"), Range(0, 1f)]
    private float scoreThreshold = 0.25f;
    [SerializeField, Tooltip("Radius for Non-Maximum Suppression, range: [0, 200]"), Range(0, 200)]
    private int nmsRadius = 70;
    [Tooltip("Max number of poses to detect for multipose detection")]
    [SerializeField] private int maxPoses = 20;
    [SerializeField, Tooltip("Use single pose decoding")]
    private bool useMultiPoseDecoding = false;
    [SerializeField, Tooltip("Minimum confidence score for an object proposal to be considered"), Range(0, 1)]
    private float confidenceThreshold = 0.5f;

    // Runtime variables
    private bool mirrorScreen = false; // Flag to check if the screen is mirrored
    private Vector2Int offset; // Offset used when cropping the input image

    private HumanPose2D[] humanPoses;


    #endregion

    #region MonoBehaviour Methods

    /// <summary>
    /// Update the InferenceController every frame, processing the input image and updating the UI and bounding boxes.
    /// </summary>
    private void Update()
    {
        // Check if all required components are valid
        if (!AreComponentsValid()) return;

        // Get the input image and dimensions
        var imageTexture = screenRenderer.material.mainTexture;
        var imageDims = new Vector2Int(imageTexture.width, imageTexture.height);
        var inputDims = imageProcessor.CalculateInputDims(imageDims, targetDim);

        // Calculate source and input dimensions for model input
        var sourceDims = inputDims;
        inputDims = modelRunner.CropInputDims(inputDims);

        // Prepare and process the input texture
        RenderTexture inputTexture = PrepareInputTexture(inputDims);
        ProcessInputImage(inputTexture, imageTexture, sourceDims, inputDims);

        // Execute the model
        modelRunner.ExecuteModel(inputTexture);
        RenderTexture.ReleaseTemporary(inputTexture);

        // Process the model outptu
        humanPoses = modelRunner.ProcessOutput(scoreThreshold, nmsRadius, maxPoses, useMultiPoseDecoding);

        // Update bounding boxes and user interface
        UpdateHumanPoses(inputDims);
        uiController.UpdateUI(humanPoses.Length);
        humanPose2DVisualizer.UpdatePoseVisualizations(humanPoses, confidenceThreshold);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Check if all required components are assigned and valid.
    /// </summary>
    /// <returns>True if all components are valid, false otherwise</returns>
    private bool AreComponentsValid()
    {
        if (imageProcessor == null || modelRunner == null || uiController == null || humanPose2DVisualizer == null)
        {
            Debug.LogError("InferenceController requires ImageProcessor, ModelRunner, and InferenceUI components.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Prepare a temporary RenderTexture with the given input dimensions.
    /// </summary>
    /// <param name="inputDims">The input dimensions for the RenderTexture</param>
    /// <returns>A temporary RenderTexture with the specified input dimensions</returns>
    private RenderTexture PrepareInputTexture(Vector2Int inputDims)
    {
        return RenderTexture.GetTemporary(inputDims.x, inputDims.y, 0, RenderTextureFormat.ARGBHalf);
    }

    /// <summary>
    /// Process the input image and apply necessary transformations.
    /// </summary>
    /// <param name="inputTexture">The input RenderTexture to process</param>
    /// <param name="imageTexture">The source image texture</param>
    /// <param name="sourceDims">The source image dimensions</param>
    /// <param name="inputDims">The input dimensions for processing</param>
    private void ProcessInputImage(RenderTexture inputTexture, Texture imageTexture, Vector2Int sourceDims, Vector2Int inputDims)
    {
        // Calculate the offset for cropping the input image
        offset = (sourceDims - inputDims) / 2;

        // Create a temporary render texture to store the cropped image
        RenderTexture sourceTexture = RenderTexture.GetTemporary(sourceDims.x, sourceDims.y, 0, RenderTextureFormat.ARGBHalf);
        Graphics.Blit(imageTexture, sourceTexture);

        // Crop and normalize the input image using Compute Shaders or fallback to Shader processing
        if (SystemInfo.supportsComputeShaders && useComputeShaders)
        {
            imageProcessor.CropImageComputeShader(sourceTexture, inputTexture, offset, inputDims);
            imageProcessor.ProcessImageComputeShader(inputTexture, "NormalizeImage");
        }
        else
        {
            ProcessImageShader(sourceTexture, inputTexture, sourceDims, inputDims);
        }

        // Release the temporary render texture
        RenderTexture.ReleaseTemporary(sourceTexture);
    }

    /// <summary>
    /// Process the input image using Shaders when Compute Shaders are not supported.
    /// </summary>
    /// <param name="sourceTexture">The source image RenderTexture</param>
    /// <param name="inputTexture">The input RenderTexture to process</param>
    /// <param name="sourceDims">The source image dimensions</param>
    /// <param name="inputDims">The input dimensions for processing</param>
    private void ProcessImageShader(RenderTexture sourceTexture, RenderTexture inputTexture, Vector2Int sourceDims, Vector2Int inputDims)
    {
        // Calculate the scaled offset and size for cropping the input image
        Vector2 scaledOffset = offset / (Vector2)sourceDims;
        Vector2 scaledSize = inputDims / (Vector2)sourceDims;

        // Create offset and size arrays for the Shader
        float[] offsetArray = new float[] { scaledOffset.x, scaledOffset.y };
        float[] sizeArray = new float[] { scaledSize.x, scaledSize.y };

        // Crop and normalize the input image using Shaders
        imageProcessor.CropImageShader(sourceTexture, inputTexture, offsetArray, sizeArray);
        imageProcessor.ProcessImageShader(inputTexture);
    }

    /// <summary>
    /// Update the human poses based on the input dimensions and screen dimensions.
    /// </summary>
    /// <param name="inputDims">The input dimensions for processing</param>
    private void UpdateHumanPoses(Vector2Int inputDims)
    {
        // Check if the screen is mirrored
        mirrorScreen = screenRenderer.transform.localScale.z == -1;

        // Get the screen dimensions
        Vector2 screenDims = new Vector2(screenRenderer.transform.localScale.x, screenRenderer.transform.localScale.y);

        // Scale and position the pose body parts based on the input and screen dimensions
        for (int i = 0; i < humanPoses.Length; i++)
        {
            for (int j = 0; j < humanPoses[i].bodyParts.Length; j++)
            {
                Vector2 coordinates = humanPoses[i].bodyParts[j].coordinates;
                humanPoses[i].bodyParts[j].coordinates = HumanPose2DUtility.ScaleBodyPartCoords(coordinates, inputDims, screenDims, offset, mirrorScreen);
            }

        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Update the confidence threshold for object detection.
    /// </summary>
    /// <param name="value">The new confidence threshold value</param>
    public void UpdateConfidenceThreshold(float value)
    {
        confidenceThreshold = value;
    }


    // Update the useMultiPoseDecoding option and the display when the Multipose toggle changes
    public void UpdateMultiposeToggle(bool useMultiPoseDecoding)
    {
        this.useMultiPoseDecoding = useMultiPoseDecoding;
    }

    #endregion
}

