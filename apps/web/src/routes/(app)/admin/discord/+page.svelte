<script lang="ts">
	import { untrack } from 'svelte';
	import { beforeNavigate } from '$app/navigation';
	import { newDiscordId, newDiscordRule, type DiscordConfiguration, type DiscordJournal, type DiscordPreview, type DiscordRule } from '$lib/discord';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	let configuration = $state<DiscordConfiguration>(untrack(() => structuredClone(data.configuration)));
	let saved = $state(untrack(() => JSON.stringify(data.configuration)));
	let journal = $state<DiscordJournal>(untrack(() => data.journal));
	let saving = $state(false);
	let busy = $state('');
	let feedback = $state('');
	let failed = $state(false);
	let preview = $state<{ ruleId: string; payload: DiscordPreview } | null>(null);
	let samples = $state<Record<string, string>>({});
	let dirty = $derived(JSON.stringify(configuration) !== saved);
	let categories = $derived([...new Set(data.events.map((event) => event.category))]);
	let knownServers = $derived(data.servers ?? []);
	let activeRules = $derived(configuration.rules.filter((rule) => rule.enabled).length);

	beforeNavigate(({ cancel }) => {
		if (dirty && !window.confirm('Les modifications Discord ne sont pas enregistrées. Quitter cette page ?')) cancel();
	});

	function beforeUnload(event: BeforeUnloadEvent) {
		if (dirty) { event.preventDefault(); event.returnValue = ''; }
	}

	async function request(path: string, body?: unknown) {
		const response = await fetch(`/api/discord/${path}`, body === undefined ? undefined : {
			method: path === 'configuration' ? 'PUT' : 'POST',
			headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
		});
		const result = await response.json().catch(() => null);
		if (!response.ok) throw new Error(result?.error ?? 'La demande a échoué. Vérifie ta connexion et réessaie.');
		return result;
	}

	async function save() {
		saving = true; feedback = '';
		try {
			configuration = await request('configuration', configuration);
			saved = JSON.stringify(configuration);
			failed = false; feedback = 'Réglages enregistrés. Ils s’appliquent aux prochains événements.';
		} catch (error) { report(error); }
		finally { saving = false; }
	}

	function report(error: unknown) {
		failed = true;
		feedback = error instanceof Error ? error.message : 'Une erreur est survenue.';
	}

	function addDestination() {
		configuration.destinations.push({ id: newDiscordId(), name: '', enabled: true, webhookUrl: '', hasWebhook: false });
	}

	function removeDestination(id: string) {
		configuration.destinations = configuration.destinations.filter((destination) => destination.id !== id);
		for (const rule of configuration.rules) rule.destinationIds = rule.destinationIds.filter((value) => value !== id);
	}

	function addRule(preset: 'custom' | 'status' | 'backups') {
		const destinations = configuration.destinations.slice(0, 1).map((destination) => destination.id);
		if (preset === 'status') {
			configuration.rules.push({ ...newDiscordRule('Serveur en ligne', ['server.started'], destinations),
				titleTemplate: '🟢 Serveur en ligne', messageTemplate: '{server} est maintenant en ligne.', color: '#6ab04c' });
			configuration.rules.push({ ...newDiscordRule('Serveur hors ligne', ['server.stopped', 'server.crashed'], destinations),
				titleTemplate: '🔴 Serveur hors ligne', messageTemplate: '{server} est maintenant hors ligne.', color: '#ef4444' });
		} else if (preset === 'backups') {
			configuration.rules.push(newDiscordRule('Sauvegardes', ['backup.completed', 'backup.failed', 'backup.cancelled'], destinations));
		} else {
			configuration.rules.push(newDiscordRule('Nouvelle règle', ['server.started'], destinations));
		}
	}

	function duplicate(rule: DiscordRule) {
		configuration.rules.push({ ...structuredClone($state.snapshot(rule)), id: newDiscordId(), name: `${rule.name.slice(0, 90)} (copie)` });
	}

	async function showPreview(rule: DiscordRule) {
		busy = `preview-${rule.id}`; feedback = '';
		try {
			preview = { ruleId: rule.id, payload: await request('preview', {
				rule, eventType: samples[rule.id] ?? rule.eventTypes[0],
				serverName: rule.serverNames[0] ?? 'serveur-exemple', username: configuration.username
			}) };
		} catch (error) { report(error); }
		finally { busy = ''; }
	}

	async function test(destinationId: string, rule?: DiscordRule) {
		busy = `test-${rule?.id ?? destinationId}`; feedback = '';
		try {
			const result = await request('test', { destinationId, ruleId: rule?.id,
				eventType: rule ? (samples[rule.id] ?? rule.eventTypes[0]) : 'server.started',
				serverName: rule?.serverNames[0] ?? 'serveur-exemple' });
			failed = !result.success; feedback = result.detail;
			journal = await request('journal');
		} catch (error) { report(error); }
		finally { busy = ''; }
	}

	async function refreshJournal() {
		busy = 'journal';
		try { journal = await request('journal'); }
		catch (error) { report(error); }
		finally { busy = ''; }
	}
</script>

<svelte:head><title>Discord — MineOS</title></svelte:head>
<svelte:window onbeforeunload={beforeUnload} />

<div class="discord-page">
	<header class="page-header">
		<div><p class="eyebrow">INTÉGRATIONS</p><h1>Discord</h1><p class="intro">Les bons messages, dans les bons salons.</p></div>
		<div class="save-area"><span class:unsaved={dirty}>{dirty ? 'Modifications non enregistrées' : 'Réglages enregistrés'}</span>
			<button class="primary" onclick={save} disabled={saving || !!busy || !dirty}>{saving ? 'Enregistrement…' : 'Enregistrer'}</button></div>
	</header>

	{#if feedback}<div class="feedback" class:error={failed} role="status">{feedback}</div>{/if}

	<fieldset class="workspace" disabled={saving || !!busy}>
		<legend class="sr-only">Réglages des notifications Discord</legend>
		<section class="panel general" aria-labelledby="general-title">
			<div><h2 id="general-title">Notifications automatiques</h2><p>Crée des règles pour chaque événement. Un événement sans règle reste silencieux.</p></div>
			<label class="switch"><input type="checkbox" bind:checked={configuration.enabled} />{configuration.enabled ? 'Activées' : 'En pause'}</label>
			<label class="sender">Nom de l’expéditeur<input maxlength="80" bind:value={configuration.username} /></label>
			<div class="summary">{configuration.destinations.length} salon(s) · {activeRules} règle(s) active(s)</div>
		</section>
		{#if !configuration.enabled}<p class="notice">Les envois automatiques sont en pause. Les tests manuels restent disponibles après enregistrement.</p>{/if}

		<section class="panel" aria-labelledby="destinations-title">
			<div class="section-heading"><div><h2 id="destinations-title">1. Salons de destination</h2><p>Ajoute un webhook pour chaque salon Discord que tu souhaites utiliser.</p></div>
				<button onclick={addDestination} disabled={configuration.destinations.length >= 20}>+ Ajouter un salon</button></div>
			<details class="help"><summary>Où trouver l’adresse du webhook ?</summary><p>Dans Discord : Modifier le salon → Intégrations → Webhooks → Nouveau webhook. Choisis le salon, puis copie l’URL du webhook ici. Aucun bot n’est nécessaire.</p><p>Les adresses enregistrées ne sont pas réaffichées. Laisse le champ vide pour conserver une adresse existante.</p></details>
			{#if configuration.destinations.length === 0}<div class="empty">Commence par ajouter ton salon de statut ou de sauvegardes.</div>{/if}
			<div class="destinations">
				{#each configuration.destinations as destination (destination.id)}
					<article class="destination">
						<div class="card-heading"><strong>#{destination.name || 'Nouveau salon'}</strong><label class="switch"><input type="checkbox" bind:checked={destination.enabled} />Actif</label></div>
						<label>Nom du salon<input placeholder="sauvegardes" maxlength="80" bind:value={destination.name} /></label>
						<label>URL du webhook<input type="password" autocomplete="new-password" spellcheck="false" maxlength="512" placeholder={destination.hasWebhook ? 'Adresse enregistrée · laisser vide pour conserver' : 'https://discord.com/api/webhooks/…'} bind:value={destination.webhookUrl} /></label>
						<div class="card-footer"><span>{destination.hasWebhook ? 'Adresse enregistrée' : 'Adresse à renseigner'}</span><div class="actions">
							<button onclick={() => test(destination.id)} disabled={dirty || !destination.hasWebhook}>{busy === `test-${destination.id}` ? 'Envoi…' : 'Envoyer un test'}</button>
							<button class="danger" onclick={() => removeDestination(destination.id)}>Retirer</button></div></div>
					</article>
				{/each}
			</div>
			{#if dirty}<p class="hint">Enregistre tes modifications pour tester les adresses et les règles.</p>{/if}
		</section>

		<section class="rules-section" aria-labelledby="rules-title">
			<div class="section-heading"><div><h2 id="rules-title">2. Messages et règles</h2><p>Choisis les événements, les serveurs concernés et les salons où envoyer chaque message.</p></div>
				<button onclick={() => addRule('custom')} disabled={configuration.destinations.length === 0 || configuration.rules.length >= 60}>+ Nouvelle règle</button></div>
			<div class="presets"><span>Départ rapide</span>
				<button onclick={() => addRule('status')} disabled={configuration.destinations.length === 0 || configuration.rules.length > 58}>Statut en ligne / hors ligne</button>
				<button onclick={() => addRule('backups')} disabled={configuration.destinations.length === 0 || configuration.rules.length >= 60}>Sauvegardes</button></div>
			<p class="hint">Ces boutons ajoutent des règles. Tu peux ensuite changer leurs salons et leurs messages. Si deux règles correspondent au même événement, chacune envoie son message.</p>
			{#if configuration.rules.length === 0}<div class="empty panel">Aucune règle. Aucun événement ne sera envoyé automatiquement.</div>{/if}
			{#each configuration.rules as rule (rule.id)}
				<article class="panel rule" class:paused={!rule.enabled}>
					<div class="card-heading"><label class="rule-name">Nom de la règle<input maxlength="100" bind:value={rule.name} /></label><label class="switch"><input type="checkbox" bind:checked={rule.enabled} />Active</label>
						<div class="actions"><button onclick={() => duplicate(rule)} disabled={configuration.rules.length >= 60}>Dupliquer</button><button class="danger" onclick={() => { configuration.rules = configuration.rules.filter((item) => item.id !== rule.id); }}>Retirer</button></div></div>
					<div class="rule-columns">
						<div>
							<h3>Quand envoyer ?</h3>
							<div class="event-groups">
								{#each categories as category}
									<fieldset class="event-group"><legend>{category}</legend>
										{#each data.events.filter((event) => event.category === category) as event}
											<label class="check"><input type="checkbox" value={event.id} bind:group={rule.eventTypes} />{event.label}</label>
										{/each}
									</fieldset>
								{/each}
							</div>
						</div>
						<div class="rule-options">
							<fieldset class="selection"><legend>Dans quels salons ?</legend>
								{#each configuration.destinations as destination}<label class="check"><input type="checkbox" value={destination.id} bind:group={rule.destinationIds} />#{destination.name || 'Sans nom'}{destination.enabled ? '' : ' (en pause)'}</label>{/each}
								{#if rule.destinationIds.length === 0}<p class="validation">Choisis au moins un salon.</p>{/if}
							</fieldset>
							<fieldset class="selection"><legend>Pour quels serveurs ?</legend><p class="hint">Aucune sélection = tous les serveurs, y compris les futurs.</p>
								{#each knownServers as server}<label class="check"><input type="checkbox" value={server.name} bind:group={rule.serverNames} />{server.displayName || server.name}</label>{/each}
								{#each rule.serverNames.filter((name) => !knownServers.some((server) => server.name === name)) as name}<label class="check"><input type="checkbox" value={name} bind:group={rule.serverNames} />{name} (indisponible)</label>{/each}
								{#if knownServers.length === 0}<p class="hint">Tes serveurs apparaîtront ici après leur création.</p>{/if}
							</fieldset>
							<label>Présentation<select bind:value={rule.format}><option value="embed">Carte Discord</option><option value="text">Texte simple</option></select></label>
							<label>Titre<input maxlength="256" bind:value={rule.titleTemplate} /></label>
							<label>Message<textarea rows="4" maxlength="4000" bind:value={rule.messageTemplate}></textarea></label>
							<p class="hint variables">Variables : <code>{'{server}'}</code> <code>{'{title}'}</code> <code>{'{message}'}</code> <code>{'{event}'}</code> <code>{'{level}'}</code> <code>{'{timestamp}'}</code></p>
							{#if rule.format === 'embed'}<div class="appearance"><label class="check"><input type="checkbox" bind:checked={rule.showServer} />Afficher le serveur</label><label class="check"><input type="checkbox" bind:checked={rule.showTimestamp} />Afficher la date</label>
								<label class="check"><input type="checkbox" checked={rule.color !== null} onchange={(event) => { rule.color = event.currentTarget.checked ? '#5865f2' : null; }} />Couleur personnalisée</label>
								{#if rule.color !== null}<label class="color-label">Couleur<input type="color" bind:value={rule.color} /></label>{/if}</div>{/if}
						</div>
					</div>
					<div class="preview-tools"><label>Événement pour l’aperçu<select value={samples[rule.id] ?? rule.eventTypes[0] ?? ''} onchange={(event) => { samples[rule.id] = event.currentTarget.value; }}>
						{#each data.events.filter((event) => rule.eventTypes.includes(event.id)) as event}<option value={event.id}>{event.category} · {event.label}</option>{/each}</select></label>
						<button onclick={() => showPreview(rule)} disabled={rule.eventTypes.length === 0}>Voir l’aperçu</button>
						<button onclick={() => test(rule.destinationIds[0], rule)} disabled={dirty || rule.destinationIds.length === 0 || rule.eventTypes.length === 0}>Tester dans le premier salon sélectionné</button></div>
					{#if preview?.ruleId === rule.id}
						<div class="discord-preview"><strong>{preview.payload.username}</strong><span class="app-badge">APP</span>
							{#if preview.payload.content}<p class="message-content">{preview.payload.content}</p>{/if}
							{#each preview.payload.embeds ?? [] as embed}<div class="embed" style:border-left-color={`#${embed.color.toString(16).padStart(6, '0')}`}><h4>{embed.title}</h4><p class="message-content">{embed.description}</p>{#each embed.fields ?? [] as field}<p><strong>{field.name}</strong><br />{field.value}</p>{/each}<small>MineOS{embed.timestamp ? ` · ${new Date(embed.timestamp).toLocaleString('fr-FR')}` : ''}</small></div>{/each}
							<p class="hint">Exemple, sans envoi. Actualise l’aperçu après une modification. Discord appliquera sa mise en forme au message reçu.</p></div>
					{/if}
				</article>
			{/each}
		</section>
	</fieldset>

	<section class="panel" aria-labelledby="journal-title">
		<div class="section-heading"><div><h2 id="journal-title">3. Historique des envois</h2><p>Les 100 derniers envois depuis le démarrage du panel. Les tests apparaissent aussi ici.</p></div><button onclick={refreshJournal} disabled={!!busy || saving}>{busy === 'journal' ? 'Actualisation…' : 'Actualiser'}</button></div>
		{#if journal.droppedEvents > 0}<p class="validation">{journal.droppedEvents} événement(s) écarté(s) car la file d’envoi était pleine.</p>{/if}
		{#if journal.entries.length === 0}<div class="empty">Aucun envoi pour le moment. Enregistre un salon, puis envoie un test.</div>{:else}<div class="journal-scroll"><table><thead><tr><th>Date</th><th>Salon</th><th>Événement</th><th>Serveur</th><th>Résultat</th></tr></thead><tbody>
			{#each journal.entries as entry}<tr><td>{new Date(entry.timestamp).toLocaleString('fr-FR')}</td><td>#{entry.destinationName}</td><td>{data.events.find((event) => event.id === entry.eventType)?.category} · {data.events.find((event) => event.id === entry.eventType)?.label ?? entry.eventType}</td><td>{entry.serverName ?? '—'}</td><td><span class:failure={!entry.success} class="result">{entry.success ? 'Envoyé' : 'Échec'}</span><small>{entry.detail}</small></td></tr>{/each}
		</tbody></table></div>{/if}
	</section>
	<p class="hint end-note">Les événements non sélectionnés ne sont pas envoyés. Les mentions de personnes et de rôles ne déclenchent aucune notification.</p>
</div>

<style>
	.discord-page { max-width: 1280px; margin: 0 auto; padding: 10px 0 40px; }
	.page-header, .section-heading, .card-heading, .card-footer, .save-area, .actions, .presets, .preview-tools { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
	.page-header, .section-heading, .card-heading, .card-footer { justify-content: space-between; }
	.page-header { margin-bottom: 28px; }
	.eyebrow { font-size: .72rem; letter-spacing: .15em; color: var(--mc-sky, #6fb5ff); margin: 0 0 8px; }
	h1 { margin: 0; font-size: 2.2rem; } h2 { font-size: 1.15rem; margin: 0 0 8px; } h3 { font-size: .95rem; margin: 0 0 14px; } h4 { margin: 0 0 8px; }
	p { line-height: 1.6; } .intro, .section-heading p, .general p { margin: 6px 0 0; color: var(--text-secondary, #b1b7ca); }
	.save-area span { font-size: .8rem; color: #aeb5c8; } .save-area .unsaved { color: #ffd287; }
	button { cursor: pointer; border: 1px solid var(--border-color, #414859); background: var(--bg-secondary, #262d3d); color: var(--text-primary, #eef0f8); border-radius: 8px; padding: 10px 14px; font: inherit; font-size: .85rem; }
	button:hover:not(:disabled) { border-color: var(--mc-sky, #6fb5ff); background: var(--bg-tertiary, #343e50); }
	button:disabled { cursor: default; opacity: .5; } .primary { background: #5865f2; border-color: #7480ff; color: white; font-weight: 600; } .danger { color: #ffb8b8; }
	button:focus-visible, input:focus-visible, select:focus-visible, textarea:focus-visible, summary:focus-visible { outline: 2px solid #89bfff; outline-offset: 3px; }
	.workspace { border: 0; padding: 0; margin: 0; min-width: 0; }
	.panel { background: var(--bg-card, rgba(25, 31, 44, .9)); border: 1px solid var(--border-color, #333c4d); border-radius: 14px; padding: 24px; margin-bottom: 24px; }
	.general { display: grid; grid-template-columns: 1fr auto; align-items: center; gap: 20px; }
	.sender { max-width: 360px; } .summary { color: #aeb5c8; font-size: .85rem; text-align: right; }
	label { display: flex; flex-direction: column; gap: 8px; font-size: .85rem; color: var(--text-secondary, #c5cada); }
	input:not([type='checkbox']):not([type='color']), select, textarea { width: 100%; box-sizing: border-box; background: var(--bg-primary, #121824); color: var(--text-primary, #eef0f8); border: 1px solid var(--border-color, #465169); padding: 11px 12px; border-radius: 7px; font: inherit; }
	textarea { resize: vertical; min-height: 90px; } input[type='checkbox'] { accent-color: #7380ff; width: 17px; height: 17px; flex-shrink: 0; margin: 0; }
	.switch, .check { flex-direction: row; align-items: center; gap: 9px; min-height: 30px; cursor: pointer; } .switch { white-space: nowrap; }
	.destinations { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 400px), 1fr)); gap: 16px; margin-top: 18px; }
	.destination { border: 1px solid var(--border-color, #414859); border-radius: 10px; padding: 18px; display: flex; flex-direction: column; gap: 16px; min-width: 0; }
	.card-heading strong { overflow-wrap: anywhere; } .card-footer span, .hint, .help, small, .end-note { font-size: .78rem; color: var(--text-secondary, #b3bbcf); line-height: 1.65; }
	.help { margin-top: 16px; } summary { cursor: pointer; color: var(--mc-sky, #6fb5ff); } .help p { max-width: 850px; }
	.empty { padding: 26px 14px; text-align: center; color: var(--text-secondary, #b3bbcf); font-size: .9rem; }
	.rules-section > .section-heading { margin-bottom: 18px; } .presets { margin: 0 0 8px; } .presets > span { font-size: .8rem; color: #aeb5c8; }
	.rule { margin-top: 20px; } .rule.paused { border-style: dashed; } .rule-name { flex: 1; min-width: 160px; max-width: 440px; }
	.rule-columns { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 28px; margin: 24px 0; }
	.event-groups { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; } .event-group { min-width: 0; border: 1px solid var(--border-color, #3a4458); border-radius: 8px; padding: 9px 12px; margin: 0; }
	legend { padding: 0 5px; font-size: .8rem; color: var(--mc-sky, #8fc7ff); } .rule-options { display: flex; flex-direction: column; gap: 14px; }
	.selection { border: 0; margin: 0; padding: 0; } .selection legend { padding: 0; font-size: .9rem; font-weight: 600; color: var(--text-primary, #eef0f8); margin-bottom: 6px; } .selection .hint { margin: 0 0 5px; }
	.appearance { display: flex; gap: 10px 20px; flex-wrap: wrap; } .color-label { flex-direction: row; align-items: center; } input[type='color'] { height: 30px; width: 45px; padding: 0; border: 0; background: none; }
	.variables { margin: 0; } code { background: #252e43; border-radius: 4px; padding: 2px 4px; display: inline-block; margin: 2px; color: #ced8ff; }
	.preview-tools { border-top: 1px solid var(--border-color, #333c4d); padding-top: 18px; align-items: end; } .preview-tools label { flex: 1; min-width: 180px; }
	.discord-preview { background: #313338; color: #f2f3f5; padding: 20px; margin-top: 18px; border-radius: 10px; overflow-wrap: anywhere; } .app-badge { background: #5865f2; color: white; font-size: .6rem; padding: 2px 4px; border-radius: 3px; margin-left: 8px; }
	.embed { border-left: 4px solid #5865f2; border-radius: 4px; padding: 15px; background: #2b2d31; margin: 12px 0; max-width: 560px; } .message-content { white-space: pre-wrap; font-size: .9rem; } .embed small { color: #b5bac1; }
	.feedback, .notice { border: 1px solid #3f7b57; background: #173329; color: #c0f3d2; padding: 14px 18px; border-radius: 9px; margin-bottom: 20px; } .feedback.error { background: #40232b; border-color: #9b4f64; color: #ffd0dc; } .notice { background: #352c1f; border-color: #80663d; color: #ffe0a9; } .validation { color: #ffb6b6; font-size: .82rem; }
	.journal-scroll { overflow-x: auto; margin-top: 20px; } table { width: 100%; border-collapse: collapse; font-size: .8rem; text-align: left; } th { color: #b3bbcf; font-weight: 500; padding: 12px; } td { border-top: 1px solid var(--border-color, #333c4d); padding: 14px 12px; vertical-align: top; } td small { display: block; max-width: 280px; } .result { color: #9be5b4; } .result.failure { color: #ffb6b6; }
	.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0, 0, 0, 0); }
	@media (max-width: 900px) { .rule-columns { grid-template-columns: 1fr; } .panel { padding: 18px; } .page-header { align-items: start; } }
	@media (max-width: 540px) { .event-groups, .general { grid-template-columns: 1fr; } .summary { text-align: left; } .save-area { width: 100%; justify-content: space-between; } .card-heading { align-items: start; } .card-heading .actions { width: 100%; } .preview-tools button { width: 100%; } h1 { font-size: 1.8rem; } }
</style>
