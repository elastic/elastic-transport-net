namespace Elastic.Transport;

/// <summary>
/// Interface for types that may be provided by clients to be used for deserialisation of the HTTP body for non-success status codes.
/// </summary>
public interface IErrorResponse
{
	/// <summary>
	/// May be called by transport to establish whether the instance represents a valid, complete error.
	/// <para>This may not always be the case if the error is partially deserialised on the response.</para>
	/// </summary>
	bool HasError();
}

/// <summary>
/// 
/// </summary>
public sealed class EmptyError : IErrorResponse
{
	/// <inheritdoc cref="IErrorResponse.HasError" />
	public bool HasError() => false;
}
