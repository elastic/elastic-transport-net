// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// Represents an endpoint <see cref="Uri"/> with additional associated metadata on which the <see cref="ITransport{TConfiguration}"/> can act.
/// </summary>
public sealed class Node : IEquatable<Node>
{
	private HashSet<string> _featureSet = [];

	/// <inheritdoc cref="Node"/>
	public Node(Uri uri, IEnumerable<string>? features = null)
	{
		// This make sures that a node can be rooted at a path to. Without the trailing slash Uri's will remove `instance` from
		// http://my-saas-provider.com/instance
		// Where this might be the user-specific path
#if NET6_0_OR_GREATER
		if (!uri.OriginalString.EndsWith('/'))
#else
		if (!uri.OriginalString.EndsWith("/", StringComparison.Ordinal))
#endif
			uri = new Uri(uri.OriginalString + "/");
		Uri = uri;
		IsAlive = true;
		Features = features is IReadOnlyCollection<string> s ? s : features?.ToList().AsReadOnly() ?? EmptyReadOnly<string>.Collection;
		IsResurrected = true;
	}

	/// <summary>
	/// A readonly collection backed by an <see cref="HashSet{T}"/> that signals what features are enabled on the node.
	/// <para> This is loosely typed as to be agnostic to what solution the transport ends up talking to </para>
	/// </summary>
	public IReadOnlyCollection<string> Features
	{
		get;
		set
		{
			field = value;
			_featureSet = [.. field];
		}
	}

	/// <summary>
	/// Settings as returned by the server, can be used in various ways later on. E.g <see cref="ITransportConfiguration.NodePredicate"/> can use it
	/// to only select certain nodes with a setting
	/// </summary>
	public IReadOnlyDictionary<string, object> Settings { get; set; } = EmptyReadOnly<string, object>.Dictionary;

	/// <summary>The id of the node defaults to null when unknown/unspecified</summary>
	public string? Id { get; internal set; }

	/// <summary>The name of the node defaults to null when unknown/unspecified</summary>
	public string? Name { get; set; }

	/// <summary> The base endpoint where the node can be reached </summary>
	public Uri Uri { get; }

	/// <summary>
	/// Indicates whether the node is alive. <see cref="ITransport{TConfiguration}"/> can take nodes out of rotation by calling
	/// <see cref="MarkDead"/> on <see cref="Node"/>.
	/// </summary>
	public bool IsAlive { get; private set; }

	/// <summary> When marked dead, this reflects the date that the node has to be taken out of rotation till</summary>
	public DateTimeOffset DeadUntil { get; private set; }

	/// <summary> The number of failed attempts trying to use this node resets when a node is marked alive</summary>
	public int FailedAttempts { get; private set; }

	/// <summary> When set, this signals the transport that a ping before first usage would be wise</summary>
	public bool IsResurrected { get; set; }

	/// <summary>
	/// Returns true if the <see cref="Features"/> has <paramref name="feature"/> enabled, or NO features are known on the node.
	/// <para>The assumption being if no <see cref="Features"/> have been discovered ALL features are enabled</para>
	/// </summary>
	public bool HasFeature(string feature) => Features.Count == 0 || _featureSet.Contains(feature);

	/// <summary>
	/// Marks this node as dead and set the date (see <paramref name="until"/>) after which we want it to come back alive
	/// </summary>
	/// <param name="until">The <see cref="DateTime"/> after which this node should be considered alive again</param>
	public void MarkDead(DateTimeOffset until)
	{
		FailedAttempts++;
		IsAlive = false;
		IsResurrected = false;
		DeadUntil = until;
	}

	/// <summary> Mark the node alive explicitly </summary>
	public void MarkAlive()
	{
		FailedAttempts = 0;
		IsAlive = true;
		IsResurrected = false;
		DeadUntil = default;
	}

	/// <summary>
	/// Use the nodes uri as root to create a <see cref="Uri"/> with <paramref name="path"/>
	/// </summary>
	public Uri CreatePath(string path) => new(Uri, path);

	/// <summary>
	/// Create a clone of the current node. This is used by <see cref="NodePool"/> implementations that supports reseeding the
	/// list of nodes through <see cref="NodePool.Reseed"/>
	/// </summary>
	public Node Clone() =>
		new(Uri, Features)
		{
			IsResurrected = IsResurrected,
			Id = Id,
			Name = Name,
			FailedAttempts = FailedAttempts,
			DeadUntil = DeadUntil,
			IsAlive = IsAlive,
			Settings = Settings,
		};

	/// <summary> Two <see cref="Node"/>'s that point to the same <see cref="Uri"/> are considered equal</summary>
	public bool Equals(Node? other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		return Uri == other.Uri;
	}

	/// <inheritdoc cref="Equals(Node)"/>
	public static bool operator ==(Node left, Node right) =>
		// ReSharper disable once MergeConditionalExpression
		ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.Equals(right);

	/// <inheritdoc cref="Equals(Node)"/>
	public static bool operator !=(Node left, Node right) => !(left == right);

	/// <inheritdoc cref="Equals(Node)"/>
	public static implicit operator Node(Uri uri) => new(uri);

	/// <inheritdoc cref="Equals(Node)"/>
	public override bool Equals(object? obj)
	{
		if (ReferenceEquals(null, obj))
			return false;
		if (ReferenceEquals(this, obj))
			return true;
		if (obj.GetType() != GetType())
			return false;

		return Equals((Node)obj);
	}

	/// <summary> A nodes identify is solely based on its <see cref="Uri"/> </summary>
	public override int GetHashCode() => Uri.GetHashCode();
}
