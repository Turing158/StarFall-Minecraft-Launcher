namespace StarFallMC.Navigation;

public interface IPageLifecycle
{
    Task ActivateAsync(CancellationToken cancellationToken);

    Task DeactivateAsync();
}
