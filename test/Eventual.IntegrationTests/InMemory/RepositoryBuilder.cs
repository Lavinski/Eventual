﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Eventual.Concurrency;
using Eventual.Domain;
using Eventual.EventStore;
using Eventual.EventStore.Implementation.InMemory;
using Eventual.EventTypes;
using Eventual.Implementation;
using Eventual.TypeDiscovery;

namespace Eventual.IntegrationTests.InMemory
{
    public class RepositoryBuilder
    {
        private EventApplyLocator applyExtensionMethods;
        private DomainEventLocator domainEvents;

        public RepositoryBuilder(params Assembly[] assemblies)
        {
            DiscoverTypes(assemblies);
            SetDefaults();
        }

        private void DiscoverTypes(Assembly[] assemblies)
        {
            applyExtensionMethods = new EventApplyLocator();
            domainEvents = new DomainEventLocator((x, i) => x.FullName.EndsWith("Event"));
            TypeDiscoveryService.DiscoverTypes(new IScanningLocator[] {applyExtensionMethods, domainEvents}, assemblies);
        }

        private void SetDefaults()
        {
            eventBus = new EventBus(x => Task.CompletedTask);
            eventStore = new InMemoryEventStore();
            transientEvents = new List<Type>();
            eventAliases = new Dictionary<Type, IReadOnlyCollection<string>>();
        }

        private IEventBus eventBus;
        private IEventStore eventStore;
        private IReadOnlyCollection<Type> transientEvents;
        private IDictionary<Type, IReadOnlyCollection<string>> eventAliases;

        public RepositoryBuilder SetEventBus(IEventBus eventBus)
        {
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));
            this.eventBus = eventBus;
            return this;
        }

        public RepositoryBuilder SetEventStore(IEventStore eventStore)
        {
            if (eventStore == null) throw new ArgumentNullException(nameof(eventStore));
            this.eventStore = eventStore;
            return this;
        }

        public RepositoryBuilder SetTransientEvents(IReadOnlyCollection<Type> transientEvents)
        {
            if (transientEvents == null) throw new ArgumentNullException(nameof(transientEvents));
            this.transientEvents = transientEvents;
            return this;
        }

        public RepositoryBuilder SetEventAliases(IDictionary<Type, IReadOnlyCollection<string>> eventAliases)
        {
            if (eventAliases == null) throw new ArgumentNullException(nameof(eventAliases));
            this.eventAliases = eventAliases;
            return this;
        }

        public IRepository<T> Build<T>()
            where T : class, IAggregateRoot
        {
            var eventClassifier = new EventClassifier(domainEvents, transientEvents, eventAliases);
            var eventApplicator = new EventApplicator(applyExtensionMethods.ApplyMethods);
            var aggregateHydrator = new AggregateHydrator<T>(eventApplicator);
            var conflictResolver = new ConflictResolver();
            var repository = new Repository<T>(eventStore, conflictResolver, aggregateHydrator, eventBus, eventClassifier);
            return repository;
        }
    }
}
