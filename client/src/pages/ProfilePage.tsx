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
          power: next.power,
          accuracy: next.accuracy,
          cueControl: next.cueControl,
          spin: next.spin,
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
        power: updated.power,
        accuracy: updated.accuracy,
        cueControl: updated.cueControl,
        spin: updated.spin,
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
        subtitle="Placeholder avatar, core stats, and editable identity settings."
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

          <Card title="Stats & Identity" subtitle="Manual stat input for initial phase">
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

              <StatInput
                label="Power"
                value={form.power}
                onChange={(value) =>
                  setForm((current) => (current ? { ...current, power: value } : current))
                }
              />
              <StatInput
                label="Accuracy"
                value={form.accuracy}
                onChange={(value) =>
                  setForm((current) => (current ? { ...current, accuracy: value } : current))
                }
              />
              <StatInput
                label="Cue control"
                value={form.cueControl}
                onChange={(value) =>
                  setForm((current) => (current ? { ...current, cueControl: value } : current))
                }
              />
              <StatInput
                label="Spin"
                value={form.spin}
                onChange={(value) =>
                  setForm((current) => (current ? { ...current, spin: value } : current))
                }
              />
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

type StatInputProps = {
  label: string
  value: number
  onChange: (value: number) => void
}

function StatInput({ label, value, onChange }: StatInputProps) {
  return (
    <label className="field-label">
      {label}
      <input
        type="number"
        min={0}
        max={100}
        step={0.5}
        value={value}
        onChange={(event) => onChange(Number(event.target.value || 0))}
      />
    </label>
  )
}
