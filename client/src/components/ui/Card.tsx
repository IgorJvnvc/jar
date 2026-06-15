import type { ReactNode } from 'react'

type CardProps = {
  title?: string
  subtitle?: string
  action?: ReactNode
  children: ReactNode
}

export function Card({ title, subtitle, action, children }: CardProps) {
  return (
    <section className="panel-card">
      {title || subtitle || action ? (
        <header className="panel-card__header">
          <div>
            {title ? <h2 className="panel-card__title">{title}</h2> : null}
            {subtitle ? <p className="panel-card__subtitle">{subtitle}</p> : null}
          </div>
          {action ? <div>{action}</div> : null}
        </header>
      ) : null}
      <div className="panel-card__content">{children}</div>
    </section>
  )
}
