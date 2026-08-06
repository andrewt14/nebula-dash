using UnityEngine;

public class PlanetEvolution : MonoBehaviour
{
    public enum EvolutionStage
    { Barren, Grassland, Forest, Village, City }

    [Header("Evolution")]
    public EvolutionStage currentStage = EvolutionStage.Barren;
    public int totalResources = 0;
    public int[] thresholds = { 0, 3, 6, 10, 15 };

    [Header("Planet Materials")]
    public Material barrenMaterial;
    public Material grassMaterial;
    public Material forestMaterial;
    public Material villageMaterial;
    public Material cityMaterial;

    private MeshRenderer planetRenderer;
    private EvolutionStage lastStage;

    void Start()
    {
        planetRenderer = GetComponent<MeshRenderer>();
        lastStage = currentStage;
    }

    void Update()
    {
        CheckEvolution();
    }

    public void AddResource(int amount = 1)
    {
        totalResources += amount;
    }

    void CheckEvolution()
    {
        if (totalResources >= thresholds[4] && currentStage != EvolutionStage.City)
            currentStage = EvolutionStage.City;
        else if (totalResources >= thresholds[3] && currentStage != EvolutionStage.Village)
            currentStage = EvolutionStage.Village;
        else if (totalResources >= thresholds[2] && currentStage != EvolutionStage.Forest)
            currentStage = EvolutionStage.Forest;
        else if (totalResources >= thresholds[1] && currentStage != EvolutionStage.Grassland)
            currentStage = EvolutionStage.Grassland;

        if (currentStage != lastStage)
        {
            TriggerEvolution();
            lastStage = currentStage;
        }
    }

    void TriggerEvolution()
    {
        Material mat = currentStage switch
        {
            EvolutionStage.Grassland => grassMaterial,
            EvolutionStage.Forest    => forestMaterial,
            EvolutionStage.Village   => villageMaterial,
            EvolutionStage.City      => cityMaterial,
            _                        => barrenMaterial
        };

        if (mat != null)
            planetRenderer.material = mat;
    }
}