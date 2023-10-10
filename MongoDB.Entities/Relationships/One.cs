﻿using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MongoDB.Entities;

/// <summary>
/// Represents a one-to-one relationship with an IEntity.
/// </summary>
/// <typeparam name="T">Any type that implements IEntity</typeparam>
public class One<T> where T : IEntity
{
    /// <summary>
    /// The Id of the entity referenced by this instance.
    /// </summary>
    [AsBsonId]
    public object ID { get; set; } = null!;

    public One()
    { }

    /// <summary>
    /// Initializes a reference to an entity in MongoDB.
    /// </summary>
    /// <param name="entity">The actual entity this reference represents.</param>
    public One(T entity)
    {
        entity.ThrowIfUnsaved();
        ID = entity.GetId();
    }

    /// <summary>
    /// Operator for returning a new One&lt;T&gt; object from a object ID
    /// </summary>
    /// <param name="id">The ID to create a new One&lt;T&gt; with</param>
    public static One<T> FromObject(object id)
        => new() { ID = id };

    /// <summary>
    /// Initializes a reference to an entity in MongoDB.
    /// </summary>
    /// <param name="id">the ID of the referenced entity</param>
    public One(string id)
    {
        ID = id;
    }

    /// <summary>
    /// Fetches the actual entity this reference represents from the database.
    /// </summary>
    /// <param name="session">An optional session</param>
    /// <param name="cancellation">An optional cancellation token</param>
    /// <returns>A Task containing the actual entity</returns>
    public Task<T> ToEntityAsync(IClientSessionHandle? session = null, CancellationToken cancellation = default)
        => new Find<T>(session, null).OneAsync(TransformID(), cancellation)!;

    /// <summary>
    /// Fetches the actual entity this reference represents from the database with a projection.
    /// </summary>
    /// <param name="projection">x => new Test { PropName = x.Prop }</param>
    /// <param name="session">An optional session if using within a transaction</param>
    /// <param name = "cancellation" > An optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown if the entity cannot be found in the database or more than one
    /// entity matching the ID is found.</exception>
    /// <returns>A Task containing the actual projected entity</returns>
    public async Task<T> ToEntityAsync(Expression<Func<T, T>> projection, IClientSessionHandle? session = null, CancellationToken cancellation = default)
        => (await new Find<T>(session, null)
                 .Match(TransformID())
                 .Project(projection)
                 .ExecuteAsync(cancellation)
                 .ConfigureAwait(false)).Single();

    /// <summary>
    /// Fetches the actual entity this reference represents from the database with a projection.
    /// </summary>
    /// <param name="projection">p=> p.Include("Prop1").Exclude("Prop2")</param>
    /// <param name="session">An optional session if using within a transaction</param>
    /// <param name = "cancellation" > An optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown if the entity cannot be found in the database or more than one
    /// entity matching the ID is found.</exception>
    /// <returns>A Task containing the actual projected entity</returns>
    public async Task<T> ToEntityAsync(Func<ProjectionDefinitionBuilder<T>, ProjectionDefinition<T, T>> projection, IClientSessionHandle? session = null, CancellationToken cancellation = default)
        => (await new Find<T>(session, null)
                 .Match(TransformID())
                 .Project(projection)
                 .ExecuteAsync(cancellation).ConfigureAwait(false)).Single();

    object TransformID()
        => ID is string { Length: 24 } vStr && ObjectId.TryParse(vStr, out var oID) ? oID : ID;
}