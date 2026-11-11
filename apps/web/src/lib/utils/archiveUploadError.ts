export function archiveUploadError(status: number, responseText = ''): string {
	if (status === 413) return 'Le fichier dépasse la taille autorisée par le serveur ou le proxy. La limite par défaut est de 10 Go.';
	if (status === 408 || status === 504) return 'Le délai de transfert a expiré. Vérifie les délais du serveur et du reverse proxy, puis réessaie.';
	if (status === 401) return 'Ta session a expiré. Reconnecte-toi avant de renvoyer le fichier.';
	if (status === 403) return 'Seul un administrateur peut envoyer une archive de serveur.';
	if (status === 0) return 'La connexion a été interrompue pendant l’envoi. Vérifie le réseau et la disponibilité du serveur.';
	try {
		const result = JSON.parse(responseText);
		if (typeof result?.error === 'string' && result.error.length > 0) return result.error.slice(0, 1000);
	} catch { /* Proxies may return an HTML error page. Show a readable message. */ }
	return `Échec de l’envoi (HTTP ${status}). Vérifie les journaux du serveur.`;
}
