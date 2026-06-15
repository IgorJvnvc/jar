import type { CSSProperties } from 'react'

type AvatarChipProps = {
  displayName: string
  colorHex: string
  ballNumber?: number | null
  size?: 'sm' | 'md' | 'lg'
}

export function AvatarChip({
  displayName,
  colorHex,
  ballNumber,
  size = 'md',
}: AvatarChipProps) {
  const initials = getInitials(displayName)

  return (
    <div className={`avatar-chip avatar-chip--${size}`} style={{ '--avatar-color': colorHex } as CSSProperties}>
      <span>{ballNumber ? String(ballNumber) : initials}</span>
    </div>
  )
}

function getInitials(displayName: string): string {
  const parts = displayName
    .trim()
    .split(/\s+/)
    .filter(Boolean)

  if (parts.length === 0) {
    return 'P'
  }

  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase()
  }

  return `${parts[0][0]}${parts[1][0]}`.toUpperCase()
}
