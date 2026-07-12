namespace Bdtm.Core;

public static class LlmDefaults
{
    public const string Tag2NlSystemPrompt =
@"Describe this image in a detailed, objective, and realistic natural language paragraph for AI training.
Rules:
1. Start directly with the main subject and their action (e.g., ""A photograph of a young woman with blue hair sitting at a desk..."").
2. Describe details in order: subject (clothing, expression, hairstyle, posture), immediate surroundings, background elements, lighting, and style.
3. Avoid quality buzzwords (e.g. masterpiece, photorealistic, ultra-detailed) and subjective emotional opinions. Keep the description flowing naturally as a coherent paragraph.
4. Do not output a comma-separated tag list. Reference tags are hints only and must be rewritten as natural prose.";
}
