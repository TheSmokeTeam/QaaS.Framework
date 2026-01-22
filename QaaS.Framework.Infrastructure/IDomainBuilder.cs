namespace QaaS.Framework.Infrastructure;

public interface IDomainBuilder<T>
{
    public T Register();
}