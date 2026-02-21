import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { useOnboarding } from '../context/OnboardingContext';
import { useApp } from '../context/AppContext';
import TourTooltip from './TourTooltip';

function Tour() {
  const { tourActive, tourStep, nextTourStep, prevTourStep, endTour, startWizard } = useOnboarding();
  const { isAdmin, currentUser } = useApp();
  const hasAdminAccess = isAdmin || currentUser?.IsAdmin;

  const [targetRect, setTargetRect] = useState(null);

  const steps = useMemo(() => {
    const baseSteps = [
      {
        selector: null,
        title: 'Welcome to Conductor',
        description: 'Conductor is a platform for managing and virtualizing AI model runners. It provides abstraction, resilience through load balancing, session affinity, multi-tenancy, and full observability of your AI infrastructure. Let\'s take a quick tour of the interface.',
        isWelcome: true
      },
      {
        selector: '[data-tour-id="nav-dashboard"]',
        title: 'Dashboard',
        description: 'Your home base. See an overview of all system resources, entity counts, and quick actions to navigate the platform.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-tenants"]',
        title: 'Tenants',
        description: 'Organizational units that group users, credentials, and resources together. Each tenant operates independently within the platform.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-users"]',
        title: 'Users',
        description: 'User accounts within a tenant. Users authenticate to access the API and can have different permission levels.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-credentials"]',
        title: 'Credentials',
        description: 'API bearer tokens for authenticating requests. Each credential is tied to a user and used to access Virtual Model Runners.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-endpoints"]',
        title: 'Model Runner Endpoints',
        description: 'Backend inference servers like Ollama, OpenAI, or other compatible services that Conductor proxies requests to.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-definitions"]',
        title: 'Model Definitions',
        description: 'Metadata describing AI models available on your endpoints, including capabilities like completions and embeddings support.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-configurations"]',
        title: 'Model Configurations',
        description: 'Parameter presets such as temperature, max tokens, and other inference settings applied to requests.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-vmr"]',
        title: 'Virtual Model Runners',
        description: 'The core abstraction. Combines endpoints, definitions, and configurations into a virtualized API with load balancing and session affinity.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-request-history"]',
        title: 'Request History',
        description: 'View and debug request/response history for Virtual Model Runners when request history is enabled.',
        position: 'right'
      },
      {
        selector: '[data-tour-id="nav-api-explorer"]',
        title: 'API Explorer',
        description: 'Interactive tool to browse and test the Conductor REST API directly from the dashboard.',
        position: 'right'
      }
    ];

    // Add admin-only nav items
    if (isAdmin) {
      baseSteps.push({
        selector: '[data-tour-id="nav-administrators"]',
        title: 'Administrators',
        description: 'Manage dashboard administrator accounts that have full system-level access.',
        position: 'right'
      });
    }
    if (hasAdminAccess) {
      baseSteps.push({
        selector: '[data-tour-id="nav-backup"]',
        title: 'Backup & Restore',
        description: 'Export and import system configuration data for backup and migration purposes.',
        position: 'right'
      });
    }

    // Header items
    baseSteps.push(
      {
        selector: '[data-tour-id="header-server"]',
        title: 'Server Connection',
        description: 'Shows which Conductor server you\'re currently connected to.',
        position: 'bottom'
      },
      {
        selector: '[data-tour-id="header-theme"]',
        title: 'Theme Toggle',
        description: 'Switch between light and dark mode to match your preference.',
        position: 'bottom'
      },
      {
        selector: '[data-tour-id="header-logout"]',
        title: 'Logout',
        description: 'Disconnect from the current Conductor server.',
        position: 'bottom'
      }
    );

    return baseSteps;
  }, [isAdmin, hasAdminAccess]);

  const currentStep = steps[tourStep];

  // Update target rect when step changes
  const updateTargetRect = useCallback(() => {
    if (!tourActive || !currentStep || currentStep.isWelcome) {
      setTargetRect(null);
      return;
    }

    const element = document.querySelector(currentStep.selector);
    if (element) {
      const rect = element.getBoundingClientRect();
      setTargetRect({
        top: rect.top,
        left: rect.left,
        width: rect.width,
        height: rect.height,
        bottom: rect.bottom,
        right: rect.right
      });
    } else {
      setTargetRect(null);
    }
  }, [tourActive, currentStep]);

  useEffect(() => {
    updateTargetRect();

    // Recalculate on scroll/resize
    window.addEventListener('resize', updateTargetRect);
    window.addEventListener('scroll', updateTargetRect, true);
    return () => {
      window.removeEventListener('resize', updateTargetRect);
      window.removeEventListener('scroll', updateTargetRect, true);
    };
  }, [updateTargetRect]);

  const handleNext = useCallback(() => {
    if (tourStep >= steps.length - 1) {
      endTour();
    } else {
      nextTourStep();
    }
  }, [tourStep, steps.length, endTour, nextTourStep]);

  const handleFinishAndWizard = useCallback(() => {
    endTour();
    setTimeout(() => startWizard(), 300);
  }, [endTour, startWizard]);

  if (!tourActive || !currentStep) return null;

  // Welcome modal (step 0)
  if (currentStep.isWelcome) {
    return (
      <div className="tour-overlay" onClick={endTour}>
        <div className="tour-welcome-modal" onClick={e => e.stopPropagation()}>
          <div className="tour-welcome-icon">
            <svg width="48" height="48" viewBox="0 0 20 20" fill="currentColor">
              <path d="M13 7H7v6h6V7z" />
              <path fillRule="evenodd" d="M7 2a1 1 0 012 0v1h2V2a1 1 0 112 0v1h2a2 2 0 012 2v2h1a1 1 0 110 2h-1v2h1a1 1 0 110 2h-1v2a2 2 0 01-2 2h-2v1a1 1 0 11-2 0v-1H9v1a1 1 0 11-2 0v-1H5a2 2 0 01-2-2v-2H2a1 1 0 110-2h1V9H2a1 1 0 010-2h1V5a2 2 0 012-2h2V2zM5 5h10v10H5V5z" clipRule="evenodd" />
            </svg>
          </div>
          <h2>Welcome to Conductor</h2>
          <p className="tour-welcome-text">
            Conductor is a platform for managing and virtualizing AI model runners. Key features include:
          </p>
          <ul className="tour-welcome-features">
            <li>Model abstraction and virtualization</li>
            <li>Resilience through load balancing</li>
            <li>Session affinity for stateful conversations</li>
            <li>Multi-tenancy for resource isolation</li>
            <li>Full observability and request history</li>
            <li>OpenAI and Ollama compatible APIs</li>
          </ul>
          <p className="tour-welcome-subtext">Let's take a quick tour of the interface to get you oriented.</p>
          <div className="tour-welcome-actions">
            <button className="btn-secondary" onClick={endTour}>Skip Tour</button>
            <button className="btn-primary" onClick={handleNext}>Start Tour</button>
          </div>
        </div>
      </div>
    );
  }

  // Final step - offer wizard
  const isLastStep = tourStep === steps.length - 1;

  return (
    <>
      {/* Overlay background */}
      <div className="tour-overlay-bg" onClick={endTour} />

      {/* Spotlight */}
      {targetRect && (
        <div
          className="tour-spotlight"
          style={{
            top: `${targetRect.top - 4}px`,
            left: `${targetRect.left - 4}px`,
            width: `${targetRect.width + 8}px`,
            height: `${targetRect.height + 8}px`
          }}
        />
      )}

      {/* Tooltip */}
      {targetRect ? (
        <TourTooltip
          targetRect={targetRect}
          title={currentStep.title}
          description={currentStep.description}
          stepIndex={tourStep - 1}
          totalSteps={steps.length - 1}
          onNext={isLastStep ? handleNext : handleNext}
          onPrev={tourStep > 1 ? prevTourStep : null}
          onSkip={endTour}
          position={currentStep.position}
        />
      ) : (
        // Fallback if element not found - show centered tooltip
        <div className="tour-overlay" onClick={endTour}>
          <div className="tour-welcome-modal" onClick={e => e.stopPropagation()}>
            <h2>{currentStep.title}</h2>
            <p className="tour-welcome-text">{currentStep.description}</p>
            <div className="tour-welcome-actions">
              <button className="btn-secondary" onClick={endTour}>Skip</button>
              <button className="btn-primary" onClick={handleNext}>
                {isLastStep ? 'Finish' : 'Next'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Post-tour wizard prompt */}
      {isLastStep && targetRect && (
        <div className="tour-wizard-prompt">
          <p>Ready to set up your first Virtual Model Runner?</p>
          <button className="btn-primary btn-small" onClick={handleFinishAndWizard}>
            Open Setup Wizard
          </button>
        </div>
      )}
    </>
  );
}

export default Tour;
