const dateFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
})

export function formatDateTime(value: string): string {
  return dateFormatter.format(new Date(value))
}

export function formatDurationFrom(startedAtUtc: string, endedAtUtc: string | null): string {
  const start = new Date(startedAtUtc).getTime()
  const end = endedAtUtc ? new Date(endedAtUtc).getTime() : Date.now()
  const minutes = Math.max(Math.floor((end - start) / 60000), 0)

  const hours = Math.floor(minutes / 60)
  const remainingMinutes = minutes % 60

  if (hours === 0) {
    return `${remainingMinutes}m`
  }

  return `${hours}h ${remainingMinutes}m`
}
