// One index belongs to one provider, model, and vector size.
public sealed record IndexedProject(ProjectDescription Project, List<float> Vector);
