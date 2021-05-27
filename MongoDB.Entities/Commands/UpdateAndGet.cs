﻿using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MongoDB.Entities
{
    /// <summary>
    /// Update and retrieve the first document that was updated.
    /// <para>TIP: Specify a filter first with the .Match(). Then set property values with .Modify() and finally call .Execute() to run the command.</para>
    /// </summary>
    /// <typeparam name="T">Any class that implements IEntity</typeparam>
    public class UpdateAndGet<T> : UpdateAndGet<T, T> where T : IEntity
    {
        internal UpdateAndGet(IClientSessionHandle session = null) : base(session) { }
    }

    /// <summary>
    /// Update and retrieve the first document that was updated.
    /// <para>TIP: Specify a filter first with the .Match(). Then set property values with .Modify() and finally call .Execute() to run the command.</para>
    /// </summary>
    /// <typeparam name="T">Any class that implements IEntity</typeparam>
    /// <typeparam name="TProjection">The type to project to</typeparam>
    public class UpdateAndGet<T, TProjection> where T : IEntity
    {
        private readonly Collection<UpdateDefinition<T>> defs = new Collection<UpdateDefinition<T>>();
        private readonly Collection<PipelineStageDefinition<T, TProjection>> stages = new Collection<PipelineStageDefinition<T, TProjection>>();
        private FilterDefinition<T> filter = Builders<T>.Filter.Empty;
        private readonly FindOneAndUpdateOptions<T, TProjection> options = new FindOneAndUpdateOptions<T, TProjection>() { ReturnDocument = ReturnDocument.After };
        private readonly IClientSessionHandle session;

        internal UpdateAndGet(IClientSessionHandle session = null)
        {
            this.session = session;
        }

        /// <summary>
        /// Specify an IEntity ID as the matching criteria
        /// </summary>
        /// <param name="ID">A unique IEntity ID</param>
        public UpdateAndGet<T, TProjection> MatchID(string ID)
        {
            return Match(f => f.Eq(t => t.ID, ID));
        }

        /// <summary>
        /// Specify the matching criteria with a lambda expression
        /// </summary>
        /// <param name="expression">x => x.Property == Value</param>
        public UpdateAndGet<T, TProjection> Match(Expression<Func<T, bool>> expression)
        {
            return Match(f => f.Where(expression));
        }

        /// <summary>
        /// Specify the matching criteria with a filter expression
        /// </summary>
        /// <param name="filter">f => f.Eq(x => x.Prop, Value) &amp; f.Gt(x => x.Prop, Value)</param>
        public UpdateAndGet<T, TProjection> Match(Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter)
        {
            this.filter &= filter(Builders<T>.Filter);
            return this;
        }

        /// <summary>
        /// Specify the matching criteria with a template
        /// </summary>
        /// <param name="template">A Template with a find query</param>
        public UpdateAndGet<T, TProjection> Match(Template template)
        {
            filter &= template.ToString();
            return this;
        }

        /// <summary>
        /// Specify a search term to find results from the text index of this particular collection.
        /// <para>TIP: Make sure to define a text index with DB.Index&lt;T&gt;() before searching</para>
        /// </summary>
        /// <param name="searchType">The type of text matching to do</param>
        /// <param name="searchTerm">The search term</param>
        /// <param name="caseSensitive">Case sensitivity of the search (optional)</param>
        /// <param name="diacriticSensitive">Diacritic sensitivity of the search (optional)</param>
        /// <param name="language">The language for the search (optional)</param>
        public UpdateAndGet<T, TProjection> Match(Search searchType, string searchTerm, bool caseSensitive = false, bool diacriticSensitive = false, string language = null)
        {
            if (searchType == Search.Fuzzy)
            {
                searchTerm = searchTerm.ToDoubleMetaphoneHash();
                caseSensitive = false;
                diacriticSensitive = false;
                language = null;
            }

            return Match(
                f => f.Text(
                    searchTerm,
                    new TextSearchOptions
                    {
                        CaseSensitive = caseSensitive,
                        DiacriticSensitive = diacriticSensitive,
                        Language = language
                    }));
        }

        /// <summary>
        /// Specify criteria for matching entities based on GeoSpatial data (longitude &amp; latitude)
        /// <para>TIP: Make sure to define a Geo2DSphere index with DB.Index&lt;T&gt;() before searching</para>
        /// <para>Note: DB.FluentGeoNear() supports more advanced options</para>
        /// </summary>
        /// <param name="coordinatesProperty">The property where 2DCoordinates are stored</param>
        /// <param name="nearCoordinates">The search point</param>
        /// <param name="maxDistance">Maximum distance in meters from the search point</param>
        /// <param name="minDistance">Minimum distance in meters from the search point</param>
        public UpdateAndGet<T, TProjection> Match(Expression<Func<T, object>> coordinatesProperty, Coordinates2D nearCoordinates, double? maxDistance = null, double? minDistance = null)
        {
            return Match(f => f.Near(coordinatesProperty, nearCoordinates.ToGeoJsonPoint(), maxDistance, minDistance));
        }

        /// <summary>
        /// Specify the matching criteria with a JSON string
        /// </summary>
        /// <param name="jsonString">{ Title : 'The Power Of Now' }</param>
        public UpdateAndGet<T, TProjection> MatchString(string jsonString)
        {
            filter &= jsonString;
            return this;
        }

        /// <summary>
        /// Specify the matching criteria with an aggregation expression (i.e. $expr)
        /// </summary>
        /// <param name="expression">{ $gt: ['$Property1', '$Property2'] }</param>
        public UpdateAndGet<T, TProjection> MatchExpression(string expression)
        {
            filter &= "{$expr:" + expression + "}";
            return this;
        }

        /// <summary>
        /// Specify the matching criteria with a Template
        /// </summary>
        /// <param name="template">A Template object</param>
        public UpdateAndGet<T, TProjection> MatchExpression(Template template)
        {
            filter &= "{$expr:" + template.ToString() + "}";
            return this;
        }

        /// <summary>
        /// Specify the property and it's value to modify (use multiple times if needed)
        /// </summary>
        /// <param name="property">x => x.Property</param>
        /// <param name="value">The value to set on the property</param>
        public UpdateAndGet<T, TProjection> Modify<TProp>(Expression<Func<T, TProp>> property, TProp value)
        {
            defs.Add(Builders<T>.Update.Set(property, value));
            return this;
        }

        /// <summary>
        /// Specify the update definition builder operation to modify the Entities (use multiple times if needed)
        /// </summary>
        /// <param name="operation">b => b.Inc(x => x.PropName, Value)</param>
        public UpdateAndGet<T, TProjection> Modify(Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> operation)
        {
            defs.Add(operation(Builders<T>.Update));
            return this;
        }

        /// <summary>
        /// Specify an update (json string) to modify the Entities (use multiple times if needed)
        /// </summary>
        /// <param name="update">{ $set: { 'RootProp.$[x].SubProp' : 321 } }</param>
        public UpdateAndGet<T, TProjection> Modify(string update)
        {
            defs.Add(update);
            return this;
        }

        /// <summary>
        /// Specify an update with a Template to modify the Entities (use multiple times if needed)
        /// </summary>
        /// <param name="template">A Template with a single update</param>
        public UpdateAndGet<T, TProjection> Modify(Template template)
        {
            Modify(template.ToString());
            return this;
        }

        /// <summary>
        /// Specify an update pipeline with multiple stages using a Template to modify the Entities.
        /// <para>NOTE: pipeline updates and regular updates cannot be used together.</para>
        /// </summary>
        /// <param name="template">A Template object containing multiple pipeline stages</param>
        public UpdateAndGet<T, TProjection> WithPipeline(Template template)
        {
            foreach (var stage in template.ToStages())
            {
                stages.Add(stage);
            }

            return this;
        }

        /// <summary>
        /// Specify an update pipeline stage to modify the Entities (use multiple times if needed)
        /// <para>NOTE: pipeline updates and regular updates cannot be used together.</para>
        /// </summary>
        /// <param name="stage">{ $set: { FullName: { $concat: ['$Name', ' ', '$Surname'] } } }</param>
        public UpdateAndGet<T, TProjection> WithPipelineStage(string stage)
        {
            stages.Add(stage);
            return this;
        }

        /// <summary>
        /// Specify an update pipeline stage using a Template to modify the Entities (use multiple times if needed)
        /// <para>NOTE: pipeline updates and regular updates cannot be used together.</para>
        /// </summary>
        /// <param name="template">A Template object containing a pipeline stage</param>
        public UpdateAndGet<T, TProjection> WithPipelineStage(Template template)
        {
            return WithPipelineStage(template.ToString());
        }

        /// <summary>
        /// Specify an array filter to target nested entities for updates (use multiple times if needed).
        /// </summary>
        /// <param name="filter">{ 'x.SubProp': { $gte: 123 } }</param>
        public UpdateAndGet<T, TProjection> WithArrayFilter(string filter)
        {
            ArrayFilterDefinition<T> def = filter;

            options.ArrayFilters =
                options.ArrayFilters == null
                ? new List<ArrayFilterDefinition>() { def }
                : options.ArrayFilters.Concat(new List<ArrayFilterDefinition> { def });

            return this;
        }

        /// <summary>
        /// Specify a single array filter using a Template to target nested entities for updates
        /// </summary>
        /// <param name="template"></param>
        public UpdateAndGet<T, TProjection> WithArrayFilter(Template template)
        {
            WithArrayFilter(template.ToString());
            return this;
        }

        /// <summary>
        /// Specify multiple array filters with a Template to target nested entities for updates.
        /// </summary>
        /// <param name="template">The template with an array [...] of filters</param>
        public UpdateAndGet<T, TProjection> WithArrayFilters(Template template)
        {
            var defs = template.ToArrayFilters<T>();

            options.ArrayFilters =
                options.ArrayFilters == null
                ? defs
                : options.ArrayFilters.Concat(defs);

            return this;
        }

        /// <summary>
        /// Specify an option for this update command (use multiple times if needed)
        /// <para>TIP: Setting options is not required</para>
        /// </summary>
        /// <param name="option">x => x.OptionName = OptionValue</param>
        public UpdateAndGet<T, TProjection> Option(Action<FindOneAndUpdateOptions<T, TProjection>> option)
        {
            option(options);
            return this;
        }

        /// <summary>
        /// Specify how to project the results using a lambda expression
        /// </summary>
        /// <param name="expression">x => new Test { PropName = x.Prop }</param>
        public UpdateAndGet<T, TProjection> Project(Expression<Func<T, TProjection>> expression)
        {
            return Project(p => p.Expression(expression));
        }

        /// <summary>
        /// Specify how to project the results using a projection expression
        /// </summary>
        /// <param name="projection">p => p.Include("Prop1").Exclude("Prop2")</param>
        public UpdateAndGet<T, TProjection> Project(Func<ProjectionDefinitionBuilder<T>, ProjectionDefinition<T, TProjection>> projection)
        {
            options.Projection = projection(Builders<T>.Projection);
            return this;
        }

        /// <summary>
        /// Run the update command in MongoDB and retrieve the first document modified
        /// </summary>
        /// <param name="cancellation">An optional cancellation token</param>
        public async Task<TProjection> ExecuteAsync(CancellationToken cancellation = default)
        {
            if (filter == Builders<T>.Filter.Empty) throw new ArgumentException("Please use Match() method first!");
            if (defs.Count == 0) throw new ArgumentException("Please use Modify() method first!");
            if (stages.Count > 0) throw new ArgumentException("Regular updates and Pipeline updates cannot be used together!");
            if (ShouldSetModDate()) Modify(b => b.CurrentDate(Cache<T>.ModifiedOnPropName));

            return await UpdateAndGetAsync(filter, Builders<T>.Update.Combine(defs), options, session, cancellation).ConfigureAwait(false);
        }

        /// <summary>
        /// Run the update command with pipeline stages and retrieve the first document modified
        /// </summary>
        /// <param name="cancellation">An optional cancellation token</param>
        public Task<TProjection> ExecutePipelineAsync(CancellationToken cancellation = default)
        {
            if (filter == Builders<T>.Filter.Empty) throw new ArgumentException("Please use Match() method first!");
            if (stages.Count == 0) throw new ArgumentException("Please use WithPipelineStage() method first!");
            if (defs.Count > 0) throw new ArgumentException("Pipeline updates cannot be used together with regular updates!");
            if (ShouldSetModDate()) WithPipelineStage($"{{ $set: {{ '{Cache<T>.ModifiedOnPropName}': new Date() }} }}");

            return UpdateAndGetAsync(filter, Builders<T>.Update.Pipeline(stages.ToArray()), options, session, cancellation);
        }

        private bool ShouldSetModDate()
        {
            //only set mod date by library if user hasn't done anything with the ModifiedOn property

            return
                Cache<T>.HasModifiedOn &&
                !defs.Any(d => d
                       .Render(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry)
                       .ToString()
                       .Contains($"\"{Cache<T>.ModifiedOnPropName}\""));
        }

        private Task<TProjection> UpdateAndGetAsync(FilterDefinition<T> filter, UpdateDefinition<T> definition, FindOneAndUpdateOptions<T, TProjection> options, IClientSessionHandle session = null, CancellationToken cancellation = default)
        {
            return session == null
                ? DB.Collection<T>().FindOneAndUpdateAsync(filter, definition, options, cancellation)
                : DB.Collection<T>().FindOneAndUpdateAsync(session, filter, definition, options, cancellation);
        }

    }
}
