import { useEffect, useState } from 'react'
import { profileApi } from '../api/profile-api'
import { shopApi } from '../api/shop-api'
import { AvatarChip } from '../components/ui/AvatarChip'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import { avatarPalette } from '../lib/palette'
import type { CueItemResponse, ProfileResponse, UpdateProfileRequest } from '../lib/types'

type ProfileFormState = UpdateProfileRequest

export function ProfilePage() {
  const authenticatedRequest = useAuthenticatedRequest()

  const [profile, setProfile] = useState<ProfileResponse | null>(null)
  const [form, setForm] = useState<ProfileFormState | null>(null)
  const [equippedCue, setEquippedCue] = useState<CueItemResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true

    const load = async () => {
      setIsLoading(true)
      setError(null)

      try {
        const { next, cues } = await authenticatedRequest(async (accessToken) => {
          const [next, cues] = await Promise.all([
            profileApi.getMine(accessToken),
            shopApi.listCues(accessToken),
          ])

          return { next, cues }
        })

        if (!active) {
          return
        }

        setProfile(next)
        setEquippedCue(cues.find((cue) => cue.isEquipped) ?? null)
        setForm({
          displayName: next.displayName,
          avatarColorHex: next.avatarColorHex,
          favoriteBallNumber: next.favoriteBallNumber,
        })
      } catch {
        if (active) {
          setError('Could not load profile.')
        }
      } finally {
        if (active) {
          setIsLoading(false)
        }
      }
    }

    void load()

    return () => {
      active = false
    }
  }, [authenticatedRequest])

  const handleSave = async () => {
    if (!form) {
      return
    }

    setFeedback(null)
    setError(null)
    setIsSubmitting(true)

    try {
      const updated = await authenticatedRequest((accessToken) => profileApi.updateMine(accessToken, form))
      setProfile(updated)
      setForm({
        displayName: updated.displayName,
        avatarColorHex: updated.avatarColorHex,
        favoriteBallNumber: updated.favoriteBallNumber,
      })
      setFeedback('Profile updated.')
    } catch {
      setError('Could not update profile.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="page-grid">
      <PageHeader
        eyebrow="Player Card"
        title="Profile"
        subtitle="Placeholder avatar, records, and editable identity settings."
      />

      {isLoading ? <p className="state-text">Loading profile...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}
      {feedback ? <p className="state-text state-text--success">{feedback}</p> : null}

      {profile && form ? (
        <>
          <div className="identity-row">
            <Card title="Crew Points" subtitle="Spend in the cue shop">
              <div className="metric-value">{profile.points}</div>
            </Card>
            <Card title="Debt" subtitle="Owed from lost duels">
              <div className="metric-value">{profile.debtPoints}</div>
            </Card>
            <Card title="Title" subtitle="Earned distinction">
              <div className="metric-chip">{profile.title ?? 'No active title'}</div>
            </Card>
            <Card title="Equipped Cue" subtitle="Active loadout">
              <div className="metric-chip">{equippedCue?.name ?? 'Default Cue'}</div>
            </Card>
          </div>

          <div className="identity-row">
            <Card title="Duel Record" subtitle="Wins vs losses in duels">
              <div className="metric-value">
                {profile.duelsWon}-{profile.duelsLost}
              </div>
              <div className="metric-chip">{winRateLabel(profile.duelsWon, profile.duelsLost)}</div>
            </Card>
            <Card title="General Record" subtitle="Games across all sessions">
              <div className="metric-value">
                {profile.gamesWon}-{profile.gamesLost}
              </div>
              <div className="metric-chip">{winRateLabel(profile.gamesWon, profile.gamesLost)}</div>
            </Card>
          </div>

          <Card title="Skill Stats" subtitle="Earned through session play; equipped cue bonuses included">
            <div className="skill-stats">
              <SkillStat
                name="Power"
                effective={profile.effectivePower}
                base={profile.power}
                bonus={equippedCue?.powerBonus ?? 0}
              />
              <SkillStat
                name="Accuracy"
                effective={profile.effectiveAccuracy}
                base={profile.accuracy}
                bonus={equippedCue?.accuracyBonus ?? 0}
              />
              <SkillStat
                name="Cue Control"
                effective={profile.effectiveCueControl}
                base={profile.cueControl}
                bonus={equippedCue?.cueControlBonus ?? 0}
              />
              <SkillStat
                name="Spin"
                effective={profile.effectiveSpin}
                base={profile.spin}
                bonus={equippedCue?.spinBonus ?? 0}
              />
            </div>
          </Card>

          <div className="two-column">
            <Card title="Avatar Placeholder" subtitle="Temporary until final style is chosen">
            <div className="avatar-editor-preview">
              <AvatarChip
                displayName={form.displayName}
                colorHex={form.avatarColorHex}
                ballNumber={form.favoriteBallNumber}
                size="lg"
              />

              <label className="field-label">
                Favorite ball number (optional)
                <input
                  type="number"
                  min={1}
                  max={15}
                  value={form.favoriteBallNumber ?? ''}
                  onChange={(event) =>
                    setForm((current) =>
                      current
                        ? {
                            ...current,
                            favoriteBallNumber: event.target.value
                              ? Number(event.target.value)
                              : null,
                          }
                        : current,
                    )
                  }
                />
              </label>
            </div>

            <div className="avatar-palette">
              {avatarPalette.map((color) => (
                <button
                  key={color}
                  type="button"
                  className={
                    form.avatarColorHex === color
                      ? 'avatar-palette__swatch avatar-palette__swatch--active'
                      : 'avatar-palette__swatch'
                  }
                  style={{ backgroundColor: color }}
                  onClick={() =>
                    setForm((current) =>
                      current
                        ? {
                            ...current,
                            avatarColorHex: color,
                          }
                        : current,
                    )
                  }
                />
              ))}
            </div>
          </Card>

          <Card title="Identity" subtitle="Your display name and how others see you">
            <div className="session-form-grid">
              <label className="field-label">
                Display name
                <input
                  value={form.displayName}
                  onChange={(event) =>
                    setForm((current) =>
                      current
                        ? {
                            ...current,
                            displayName: event.target.value,
                          }
                        : current,
                    )
                  }
                />
              </label>
            </div>

            <button type="button" className="button button--primary" onClick={handleSave} disabled={isSubmitting}>
              {isSubmitting ? 'Saving...' : 'Save Profile'}
            </button>
          </Card>
          </div>
        </>
      ) : null}
    </div>
  )
}

function winRateLabel(won: number, lost: number): string {
  const played = won + lost

  if (played === 0) {
    return 'No record yet'
  }

  return `${Math.round((won / played) * 100)}% win rate`
}

function formatStat(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1)
}

function clampPercent(value: number): number {
  return Math.max(0, Math.min(100, value))
}

function SkillStat({
  name,
  effective,
  base,
  bonus,
}: {
  name: string
  effective: number
  base: number
  bonus: number
}) {
  return (
    <div className="skill-stat">
      <div className="skill-stat__head">
        <span className="skill-stat__name">{name}</span>
        <span className="skill-stat__value">{formatStat(effective)}</span>
      </div>
      <div className="skill-stat__bar">
        <div className="skill-stat__bar-fill" style={{ width: `${clampPercent(effective)}%` }} />
      </div>
      <span className="skill-stat__detail">
        Base {formatStat(base)}
        {bonus !== 0 ? ` \u00b7 Cue ${bonus > 0 ? '+' : ''}${formatStat(bonus)}` : ''}
      </span>
    </div>
  )
}
