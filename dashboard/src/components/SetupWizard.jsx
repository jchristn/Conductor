import React, { useEffect, useCallback, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useOnboarding } from '../context/OnboardingContext';
import Modal from './Modal';

const WIZARD_STEPS = [
  {
    key: 'intro',
    title: 'Getting Started',
    description: 'This wizard will walk you through creating the entities needed for a working Virtual Model Runner. Each step creates one resource, building toward a fully configured virtualized AI endpoint.',
    entityType: null
  },
  {
    key: 'tenant',
    title: 'Create a Tenant',
    description: 'Tenants are organizational units that group users, credentials, and resources. Everything in Conductor belongs to a tenant. Create your first tenant to get started.',
    entityType: 'tenant',
    route: '/tenants',
    buttonLabel: 'Create Tenant'
  },
  {
    key: 'user',
    title: 'Create a User',
    description: 'Users belong to tenants and authenticate to use the API. Create a user account that will be associated with your tenant.',
    entityType: 'user',
    route: '/users',
    buttonLabel: 'Create User'
  },
  {
    key: 'credential',
    title: 'Create a Credential',
    description: 'Credentials are API bearer tokens that users present to authenticate requests. Create a credential for your user to access Virtual Model Runners.',
    entityType: 'credential',
    route: '/credentials',
    buttonLabel: 'Create Credential'
  },
  {
    key: 'endpoint',
    title: 'Add a Model Runner Endpoint',
    description: 'Connect Conductor to a backend inference server such as Ollama, OpenAI, or any compatible service. This is where your AI models actually run.',
    entityType: 'endpoint',
    route: '/endpoints',
    buttonLabel: 'Add Endpoint'
  },
  {
    key: 'definition',
    title: 'Create a Model Definition',
    description: 'Describe a model that exists on your endpoint. Model definitions capture metadata like the model name, family, and what capabilities it supports.',
    entityType: 'definition',
    route: '/definitions',
    buttonLabel: 'Create Definition'
  },
  {
    key: 'configuration',
    title: 'Create a Model Configuration',
    description: 'Set default parameters for inference requests such as temperature, max tokens, and other settings that control model behavior.',
    entityType: 'configuration',
    route: '/configurations',
    buttonLabel: 'Create Configuration'
  },
  {
    key: 'vmr',
    title: 'Create a Virtual Model Runner',
    description: 'The final piece! Combine your endpoints, definitions, and configurations into a virtualized API endpoint with load balancing, session affinity, and more.',
    entityType: 'vmr',
    route: '/vmr',
    buttonLabel: 'Create VMR'
  },
  {
    key: 'complete',
    title: 'Setup Complete!',
    description: 'Congratulations! You\'ve set up a complete Virtual Model Runner pipeline. You can now use the API Explorer to test your setup, or use the VMR\'s API endpoint from any compatible client.',
    entityType: null
  }
];

function SetupWizard() {
  const {
    wizardActive,
    wizardStep,
    nextWizardStep,
    prevWizardStep,
    skipWizardStep,
    endWizard,
    setPendingCreate,
    lastCreatedEntity,
    setLastCreatedEntity
  } = useOnboarding();
  const navigate = useNavigate();

  const currentStep = WIZARD_STEPS[wizardStep];
  const totalSteps = WIZARD_STEPS.length;

  // Track step statuses
  const stepStatuses = useMemo(() => {
    return WIZARD_STEPS.map((step, index) => {
      if (index < wizardStep) return 'done';
      if (index === wizardStep) return 'active';
      return 'pending';
    });
  }, [wizardStep]);

  // Auto-advance when entity is created
  useEffect(() => {
    if (lastCreatedEntity && currentStep && currentStep.entityType === lastCreatedEntity) {
      setLastCreatedEntity(null);
      nextWizardStep();
    }
  }, [lastCreatedEntity, currentStep, nextWizardStep, setLastCreatedEntity]);

  const handleCreateEntity = useCallback(() => {
    if (!currentStep || !currentStep.route) return;
    navigate(currentStep.route);
    setPendingCreate(currentStep.entityType);
  }, [currentStep, navigate, setPendingCreate]);

  const handleGoToExplorer = useCallback(() => {
    navigate('/api-explorer');
    endWizard();
  }, [navigate, endWizard]);

  if (!wizardActive) return null;

  return (
    <Modal isOpen={wizardActive} onClose={endWizard} title="Setup Wizard" extraWide>
      <div className="wizard-layout">
        {/* Left panel - step list */}
        <div className="wizard-step-list">
          {WIZARD_STEPS.map((step, index) => (
            <div
              key={step.key}
              className={`wizard-step-item ${stepStatuses[index]}`}
            >
              <div className="wizard-step-indicator">
                {stepStatuses[index] === 'done' ? (
                  <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                ) : (
                  <span>{index + 1}</span>
                )}
              </div>
              <span className="wizard-step-label">{step.title}</span>
            </div>
          ))}
        </div>

        {/* Right panel - step content */}
        <div className="wizard-content">
          <div className="wizard-content-header">
            <h3>{currentStep.title}</h3>
            <span className="wizard-progress">Step {wizardStep + 1} of {totalSteps}</span>
          </div>

          <p className="wizard-description">{currentStep.description}</p>

          {/* Intro step */}
          {currentStep.key === 'intro' && (
            <div className="wizard-intro-summary">
              <p>You'll create the following resources in order:</p>
              <ol className="wizard-entity-list">
                <li>Tenant - organizational unit</li>
                <li>User - account for authentication</li>
                <li>Credential - API token</li>
                <li>Model Runner Endpoint - backend server</li>
                <li>Model Definition - model metadata</li>
                <li>Model Configuration - parameter presets</li>
                <li>Virtual Model Runner - virtualized API</li>
              </ol>
              <p className="wizard-intro-note">You can skip any step and create entities later.</p>
            </div>
          )}

          {/* Entity create steps */}
          {currentStep.entityType && (
            <div className="wizard-create-section">
              <button className="btn-primary" onClick={handleCreateEntity}>
                {currentStep.buttonLabel}
              </button>
              <p className="wizard-create-hint">
                Clicking this will navigate to the {currentStep.title.replace('Create a ', '').replace('Add a ', '').replace('Create a ', '')} page and open the create form.
                The wizard will auto-advance once you save.
              </p>
            </div>
          )}

          {/* Complete step */}
          {currentStep.key === 'complete' && (
            <div className="wizard-complete-section">
              <div className="wizard-complete-icon">
                <svg width="48" height="48" viewBox="0 0 20 20" fill="currentColor">
                  <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                </svg>
              </div>
              <button className="btn-primary" onClick={handleGoToExplorer}>
                Open API Explorer
              </button>
            </div>
          )}

          {/* Navigation */}
          <div className="wizard-actions">
            <div className="wizard-actions-left">
              {wizardStep > 0 && currentStep.key !== 'complete' && (
                <button className="btn-secondary btn-small" onClick={prevWizardStep}>
                  Back
                </button>
              )}
            </div>
            <div className="wizard-actions-right">
              {currentStep.entityType && (
                <button className="btn-secondary btn-small" onClick={skipWizardStep}>
                  Skip
                </button>
              )}
              {currentStep.key === 'intro' && (
                <button className="btn-primary btn-small" onClick={nextWizardStep}>
                  Begin
                </button>
              )}
              {currentStep.key === 'complete' && (
                <button className="btn-secondary btn-small" onClick={endWizard}>
                  Close
                </button>
              )}
            </div>
          </div>
        </div>
      </div>
    </Modal>
  );
}

export default SetupWizard;
